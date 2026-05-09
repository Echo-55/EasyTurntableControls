using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KeyValue.Runtime;
using EasyTurntableControls.Components;
using EasyTurntableControls.WindowStates;
using Track;
using UI;
using UI.Builder;
using UI.Common;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace EasyTurntableControls;

public class EasyTurntableControlsWindow : MonoBehaviour, IProgrammaticWindow
{
    private readonly ILogger _logger = Serilog.Log.ForContext<EasyTurntableControlsWindow>();

    /// <summary>Angle error (degrees) below which rotation is considered complete.</summary>
    private const float AngleStopThreshold = 0.2f;

    private const float CenterLeverValue = 0.5f;

    private Window? _window;
    private UIPanel? _panel;
    private TurntableController? _activeTurntableController;
    private FieldInfo? _keyValueObjectFieldInfo;
    private Coroutine? _rotationCoroutine;
    private int _selectedTrackIndex;
    private bool _settingsDirty;
    private TurntableNodesVisualizer? _nodesVisualizer;

    private SearchState? _searchState;
    private SettingsState? _settingsState;
    private ControlState? _controlState;
    private readonly StateMachine _stateMachine = new();

    public bool IsShown => _window != null && _window.IsShown;

    public UIBuilderAssets? BuilderAssets { get; set; }
    public string WindowIdentifier => "TurntableControlWindow";
    public Vector2Int DefaultSize { get; } = new(300, 500);
    public Window.Position DefaultPosition => Window.Position.Center;

    public Window.Sizing Sizing { get; } =
        Window.Sizing.Resizable(new Vector2Int(10, 500), new Vector2Int(300, 500));

    private enum WindowMode
    {
        Control,
        Search,
        Settings
    }

    private WindowMode _mode = WindowMode.Control;
    private WindowMode _previousMode = WindowMode.Control;
    private List<TurntableController>? _allTurntables;
    private Camera? _mainCamera;

    public void Show(TurntableController? turntableController, List<TurntableController>? allTurntables,
        Camera? mainCamera = null)
    {
        _mainCamera = mainCamera;
        _allTurntables = allTurntables;

        if (turntableController != null)
        {
            _mode = WindowMode.Control;
            _activeTurntableController = turntableController;
        }
        else
        {
            _mode = WindowMode.Search;
            _activeTurntableController = null;
        }

        _window ??= GetComponent<Window>();
        if (_window == null)
        {
            _logger.Error("Window component is missing.");
            return;
        }

        Populate(_window);
        _window.ShowWindow();
        _selectedTrackIndex = _activeTurntableController?.turntable.StopIndex ?? 0;
    }

    public void Hide() => CleanupState(false, false);

    public void Populate(Window window)
    {
        window.Title = "Turntable Control Window";
        _panel?.Dispose();
        _panel = UIPanel.Create(window.contentRectTransform, BuilderAssets, BuildPanel);
    }

    public void OnWorldLoaded()
    {
        _keyValueObjectFieldInfo = AccessTools.Field(typeof(TurntableController), "_propertyObject");

        if (_keyValueObjectFieldInfo == null)
            _logger.Error("Failed to find TurntableController._propertyObject via reflection.");
    }

    public void OnWorldUnloaded() { CleanupState(true, true); }

    public void Cleanup()
    {
        CleanupState(true, true);
        _panel?.Dispose();
        _panel = null;
        _window = null;
    }

    private void OnDestroy() => Cleanup();

    private void BuildPanel(UIPanelBuilder builder)
    {
        switch (_mode)
        {
            case WindowMode.Settings:
                _settingsState ??= CreateSettingsState();
                if (_stateMachine.CurrentState != _settingsState)
                    _stateMachine.TransitionTo(_settingsState);
                _stateMachine.BuildCurrentState(builder);
                return;
            case WindowMode.Search:
                _searchState ??= CreateSearchState();
                if (_stateMachine.CurrentState != _searchState)
                    _stateMachine.TransitionTo(_searchState);
                _stateMachine.BuildCurrentState(builder);
                return;
            case WindowMode.Control:
            default:
                _controlState ??= CreateControlState();
                if (_stateMachine.CurrentState != _controlState)
                    _stateMachine.TransitionTo(_controlState);
                _stateMachine.BuildCurrentState(builder);
                return;
        }
    }

    private float GetControlLeverValueForActiveTurntable()
    {
        return TryGetKeyValueObject(_activeTurntableController, out var kvpObject)
            ? kvpObject["controlLever"].FloatValue
            : CenterLeverValue;
    }

    private void SetControlLeverValueForActiveTurntable(float value) { SetControlLeverForTurntable(_activeTurntableController, value); }

    private void UpdateSettings(System.Action<Main.EasyTurntableControlsSettings> update)
    {
        var settings = Main.Settings;
        if (settings == null) return;

        update(settings);
        _settingsDirty = true;
    }

    private void OpenSettings()
    {
        if (_mode != WindowMode.Settings)
            _previousMode = _mode;

        _mode = WindowMode.Settings;
        _stateMachine.TransitionTo(_settingsState ??= CreateSettingsState());
        RefreshWindow();
    }

    private void CloseSettings()
    {
        _stateMachine.ClearCurrent();
        _mode = _previousMode;
        RefreshWindow();
    }

    private void ResetEditableSettings()
    {
        var settings = Main.Settings;
        if (settings == null) return;

        settings.ResetWindowEditableSettings();
        _settingsDirty = true;
        RefreshWindow();
    }

    private void SavePendingSettings()
    {
        if (!_settingsDirty) return;

        Main.SaveSettings();
        _settingsDirty = false;
    }

    private void RefreshWindow()
    {
        if (_window != null)
            Populate(_window);
    }

    private void StartRotation(IEnumerator coroutine)
    {
        StopRotation(true);
        _rotationCoroutine = StartCoroutine(coroutine);
    }

    private void StartRotateToIndex(int idx)
    {
        if (_activeTurntableController == null) return;

        StartRotation(Co_RotateToAngle(
            _activeTurntableController,
            _activeTurntableController.turntable.AngleForIndex(idx)));
    }

    private void StartRotateToNextPosition(int direction)
    {
        if (_activeTurntableController == null) return;
        var turntable = _activeTurntableController.turntable;
        var subdivisions = turntable.subdivisions;
        var currentIndex = turntable.IndexAndRemainderForAngle(out _);
        var targetIndex = direction > 0
            ? (currentIndex + 1) % subdivisions
            : (currentIndex - 1 + subdivisions) % subdivisions;
        StartRotation(Co_RotateToAngle(_activeTurntableController, turntable.AngleForIndex(targetIndex)));
    }

    private void FlipTurntable()
    {
        if (_activeTurntableController == null) return;
        var targetAngle = (_activeTurntableController.turntable.Angle + 180f) % 360f;
        StartRotation(Co_RotateToAngle(_activeTurntableController, targetAngle));
    }

    /// <summary>
    /// Coroutine that PID-drives the turntable to a given absolute angle, then re-centers the lever.
    /// </summary>
    private IEnumerator Co_RotateToAngle(TurntableController? turntableController, float targetAngle)
    {
        if (turntableController == null) yield break;
        var settings = Main.Settings;
        if (settings == null) yield break;
        if (!TryGetKeyValueObject(turntableController, out KeyValueObject kvpObject)) yield break;

        var turntable = turntableController.turntable;
        var pid = new PidController(settings.PidSettings);
        pid.Reset();

        while (true)
        {
            var currentAngle = turntable.Angle;
            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) < AngleStopThreshold) break;

            var output = pid.Update(targetAngle, currentAngle, Time.deltaTime);
            kvpObject["controlLever"] = Value.Float(CenterLeverValue + Mathf.Clamp(output, -1f, 1f) * 0.5f);
            yield return null;
        }

        kvpObject["controlLever"] = Value.Float(CenterLeverValue);
        _rotationCoroutine = null;
    }

    private void JumpToTurntable(TurntableController t)
    {
        if (_mainCamera == null) return;
        _mainCamera.transform.position = t.transform.position + new Vector3(0, 10, 0);
        _mainCamera.transform.LookAt(t.transform);
        Show(t, _allTurntables, _mainCamera);
    }

    private void ToggleNodesVisualizer()
    {
        if (_activeTurntableController == null) return;

        if (_nodesVisualizer == null)
        {
            _nodesVisualizer = gameObject.AddComponent<TurntableNodesVisualizer>();
            _nodesVisualizer.Show(_activeTurntableController);
            return;
        }

        _nodesVisualizer.Hide();
        Destroy(_nodesVisualizer);
        _nodesVisualizer = null;
    }

    private ControlState CreateControlState()
    {
        return new ControlState(() => _activeTurntableController,
            () => _selectedTrackIndex,
            idx => _selectedTrackIndex = idx,
            StartRotateToIndex,
            () => _nodesVisualizer == null ? "Show Nodes" : "Hide Nodes",
            ToggleNodesVisualizer,
            GetControlLeverValueForActiveTurntable,
            SetControlLeverValueForActiveTurntable,
            StartRotateToNextPosition,
            FlipTurntable,
            OpenSettings);
    }

    private SearchState CreateSearchState()
    {
        return new SearchState(() => _allTurntables,
            () => _mainCamera,
            OpenSettings,
            JumpToTurntable);
    }

    private SettingsState CreateSettingsState()
    {
        return new SettingsState(
            _stateMachine,
            () => Main.Settings,
            UpdateSettings,
            CloseSettings,
            ResetEditableSettings,
            SavePendingSettings);
    }

    private void CleanupState(bool clearReflectionState, bool resetWindowMode)
    {
        _stateMachine.ClearCurrent();
        SavePendingSettings();
        StopRotation(resetLever: true);
        HideNodesVisualizer();
        _window?.CloseWindow();
        _selectedTrackIndex = -1;
        _activeTurntableController = null;
        _allTurntables = null;
        _mainCamera = null;

        if (resetWindowMode)
        {
            _mode = WindowMode.Control;
            _previousMode = WindowMode.Control;
        }

        if (clearReflectionState)
            _keyValueObjectFieldInfo = null;
    }

    private void HideNodesVisualizer()
    {
        if (_nodesVisualizer == null) return;

        _nodesVisualizer.Hide();
        Destroy(_nodesVisualizer);
        _nodesVisualizer = null;
    }

    private void StopRotation(bool resetLever)
    {
        if (_rotationCoroutine != null)
        {
            StopCoroutine(_rotationCoroutine);
            _rotationCoroutine = null;
        }

        if (!resetLever) return;
        var turntableController = _activeTurntableController;
        SetControlLeverForTurntable(turntableController, CenterLeverValue);
    }

    private void SetControlLeverForTurntable(TurntableController? turntableController, float value)
    {
        if (!TryGetKeyValueObject(turntableController, out var kvpObject)) return;

        kvpObject["controlLever"] = Value.Float(value);
    }

    private bool TryGetKeyValueObject(TurntableController? turntableController, out KeyValueObject kvpObject)
    {
        kvpObject = null!;

        if (turntableController == null || _keyValueObjectFieldInfo == null)
            return false;

        var value = _keyValueObjectFieldInfo.GetValue(turntableController) as KeyValueObject;
        if (value == null)
        {
            _logger.Warning("Unable to access turntable control lever state for {TurntableName}.",
                turntableController.name);
            return false;
        }

        kvpObject = value;
        return true;
    }
}