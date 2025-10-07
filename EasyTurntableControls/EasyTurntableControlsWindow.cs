using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KeyValue.Runtime;
using EasyTurntableControls.Components;
using Track;
using UI;
using UI.Builder;
using UI.Common;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace EasyTurntableControls;

public class EasyTurntableControlsWindow : MonoBehaviour, IProgrammaticWindow
{
    private ILogger _logger = Serilog.Log.ForContext<EasyTurntableControlsWindow>();

    /// <summary>
    /// Reference to the window component.
    /// This is used to show or hide the window.
    /// </summary>
    private Window? _window;

    /// <summary>
    /// Reference to the panel component.
    /// This is used to build the UI elements within the window.
    /// </summary>
    private UIPanel? _panel;

    /// <summary>
    /// Reference to the active TurntableController.
    /// This is the turntable controller that the window is currently controlling.
    /// It is set when the window is shown and cleared when the world is unloaded.
    /// If the TurntableController is null, the window will not function correctly.
    /// This field is used to access the turntable's properties and control its behavior.
    /// </summary>
    private TurntableController? _activeTurntableController;

    /// <summary>
    /// FieldInfo to access the KeyValueObject in the TurntableController.
    /// This is used to manipulate the control lever value for the turntable.
    /// It is set during the OnWorldLoaded event.
    /// </summary>
    private FieldInfo? _keyValueObjectFieldInfo;

    /// <summary>
    /// Coroutine for handling rotation to a specific track index.
    /// This is used to smoothly rotate the turntable to the selected track.
    /// It is set when a rotation action is initiated and cleared when the action completes.
    /// If a new rotation action is initiated while this coroutine is running, it will be stopped and replaced with the new action.
    /// </summary>
    private Coroutine? _rotationCoroutine;

    /// <summary>
    /// The index of the currently selected track.
    /// This is used to keep track of which track the user has selected in the UI.
    /// It is initialized to 0, meaning the first track is selected by default.
    /// The value is updated when the user selects a different track from the dropdown menu.
    /// It is used when the user clicks the "Go" button to rotate the turntable
    /// </summary>
    private int _selectedTrackIndex;

    private TurntableNodesVisualizer? _nodesVisualizer;

    public bool IsShown => _window != null && _window.IsShown;

    /// <summary>
    /// Gets or sets the UIBuilderAssets used for building the window UI.
    /// From IProgrammaticWindow interface.
    /// </summary>
    public UIBuilderAssets? BuilderAssets { get; set; }

    /// <summary>
    /// Gets the unique identifier for the window.
    /// From IProgrammaticWindow interface.
    /// </summary>
    public string WindowIdentifier => "TurntableControlWindow";

    /// <summary>
    /// Gets the default size of the window.
    /// From IProgrammaticWindow interface.
    /// </summary>
    public Vector2Int DefaultSize { get; } = new(300, 500);

    /// <summary>
    /// Gets the default position of the window.
    /// From IProgrammaticWindow interface.
    /// </summary>
    public Window.Position DefaultPosition => Window.Position.Center;

    /// <summary>
    /// Determines how the window should be sized.
    /// This specifies that the window is resizable with minimum and maximum size constraints.
    /// From IProgrammaticWindow interface.
    /// </summary>
    public Window.Sizing Sizing { get; } =
        Window.Sizing.Resizable(new Vector2Int(10, 500), new Vector2Int(300, 500));

    public enum WindowMode
    {
        Control,
        List
    }

    private WindowMode _mode = WindowMode.Control;
    private List<TurntableController>? _allTurntables = new();
    private Camera? _mainCamera;

    public void Show(TurntableController? turntableController, List<TurntableController>? allTurntables,
        Camera? mainCamera = null)
    {
        _mainCamera = mainCamera;
        if (turntableController != null)
        {
            _mode = WindowMode.Control;
            _activeTurntableController = turntableController;
        }
        else
        {
            _mode = WindowMode.List;
            _allTurntables = allTurntables;
            _activeTurntableController = null;
        }

        _window ??= GetComponent<Window>();
        if (_window == null)
        {
            _logger.Error("Window component is missing.");
            return;
        }

        Populate(_window);
        _window?.ShowWindow();
        _selectedTrackIndex = _activeTurntableController?.turntable.StopIndex ?? 0;
    }

    public void Hide()
    {
        _window?.CloseWindow();
        _selectedTrackIndex = -1;

        _activeTurntableController = null;

        if (_rotationCoroutine != null)
        {
            StopCoroutine(_rotationCoroutine);
            _rotationCoroutine = null;
        }

        if (_nodesVisualizer != null)
        {
            _nodesVisualizer.Hide();
            Destroy(_nodesVisualizer);
            _nodesVisualizer = null;
        }
    }

    public void Populate(Window window)
    {
        window.Title = "Turntable Control Window";

        _panel?.Dispose();
        _panel = UIPanel.Create(window.contentRectTransform, BuilderAssets, BuildPanel);
    }

    /// <summary>
    /// Called when the world is loaded.
    /// Initializes the FieldInfo for accessing the KeyValueObject in the TurntableController.
    /// </summary>
    public void OnWorldLoaded()
    {
        _keyValueObjectFieldInfo = AccessTools.Field(typeof(TurntableController), "_propertyObject");
    }

    /// <summary>
    /// Called when the world is unloaded.
    /// Clears the reference to the active turntable controller.
    /// </summary>
    public void OnWorldUnloaded() { _activeTurntableController = null; }

    /// <summary>
    /// Builds the UI panel for the Turntable Control Window.
    /// This method defines the layout and content of the window using the provided UIPanelBuilder.
    /// </summary>
    /// <param name="builder">The UIPanelBuilder used to construct the UI elements.</param>
    private void BuildPanel(UIPanelBuilder builder)
    {
        if (_mode == WindowMode.List)
        {
            builder.AddTitle("Turntable Selection", "");
            builder.Spacer(10f);
            if (_allTurntables == null || _allTurntables.Count == 0)
            {
                builder.AddLabel("No turntables found in the world.");
                return;
            }

            builder.AddLabel("No turntable in range. Select one to jump to:");
            builder.Spacer(5f);
            builder.HScrollView(b =>
            {
                foreach (var t in _allTurntables)
                {
                    var tName = t.name;
                    float dist = _mainCamera != null
                        ? Vector3.Distance(_mainCamera.transform.position, t.transform.position)
                        : 0f;
                    b.HStack(h =>
                    {
                        h.AddLabel($"{tName} ({dist:F1}m)");
                        h.AddButtonCompact("Jump to", () => JumpToTurntable(t));
                    });
                }
            }, new RectOffset(5, 5, 5, 5));
            return;
        }

        if (_activeTurntableController == null)
        {
            builder.AddTitle("Turntable Control Window", "No Turntable Controller");
            builder.Spacer(10f);
            builder.AddLabel("No turntable controller found in range.");
            builder.Spacer(10f);
            return;
        }

        builder.AddTitle("Controlling Turntable: ", _activeTurntableController.turntable.name);
        builder.Spacer(10f);

        // Track Selection Section
        builder.AddSection("Track Selection", b =>
        {
            var trackCount = _activeTurntableController.turntable.subdivisions;
            var trackOptions = Enumerable.Range(0, trackCount).Select(i => $"Track {i}").ToList();
            var currentTrack = _activeTurntableController.turntable.StopIndex ?? 0;

            b.AddField("Track", b.AddDropdown(trackOptions, currentTrack, idx =>
            {
                _selectedTrackIndex = idx;
                StartRotateToIndex(idx);
            }));
            b.AddButtonCompact("Go", () => StartRotateToIndex(_selectedTrackIndex));
        });

        // Nodes Visualization Toggle
        builder.AddButtonCompact(
            () => _nodesVisualizer == null ? "Show Nodes" : "Hide Nodes",
            () =>
            {
                if (_nodesVisualizer == null)
                {
                    _nodesVisualizer = gameObject.AddComponent<TurntableNodesVisualizer>();
                    _nodesVisualizer.Show(_activeTurntableController);
                }
                else
                {
                    _nodesVisualizer.Hide();
                    Destroy(_nodesVisualizer);
                    _nodesVisualizer = null;
                }
            }
        );

        builder.Spacer(10f);

        // Speed Controls Section
        builder.AddSection("Speed Controls", b =>
        {
            b.AddSliderQuantized(
                valueClosure: () =>
                {
                    var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(_activeTurntableController);
                    return kvpObject["controlLever"].FloatValue;
                },
                textValueClosure: () =>
                {
                    var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(_activeTurntableController);
                    var controlLeverValue = kvpObject["controlLever"].FloatValue;
                    return $"{Mathf.Lerp(-100f, 100f, controlLeverValue):F0}%";
                },
                valueChangedAction: SetControlLever,
                minValue: 0f,
                maxValue: 1f,
                increment: 0.01f,
                editingEndedAction: _ => SetControlLever(0.5f) // Center lever when editing ends
            );

            b.HStack(h =>
            {
                h.AddButtonCompact("\u25C4 Rotate Left", () => StartRotateToNextPosition(-1));
                h.AddButtonCompact("Rotate Right \u25BA", () => StartRotateToNextPosition(1));
            });

            // Flip 180° button
            b.AddButtonCompact("Flip 180°", FlipTurntable);

            b.Spacer(5f);

            // Current speed display
            b.AddField("Current Speed", () =>
            {
                var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(_activeTurntableController);
                var controlLeverValue = kvpObject["controlLever"].FloatValue;
                var displayValue = Mathf.Lerp(-100f, 100f, controlLeverValue);
                return $"{displayValue:F0}%";
            }, UIPanelBuilder.Frequency.Fast);
        });

        builder.Spacer(10f);

        // Status Section
        builder.AddSection("Status", b =>
        {
            b.AddField("Current Angle", () => $"{_activeTurntableController.turntable.Angle:F1}°",
                UIPanelBuilder.Frequency.Fast);
            b.AddField("Current Track", () => $"{_activeTurntableController.turntable.StopIndex ?? -1}",
                UIPanelBuilder.Frequency.Fast);
            b.AddField("Status", () => _activeTurntableController.turntable.IsLined ? "Lined" : "Not Lined",
                UIPanelBuilder.Frequency.Fast);
        });
    }

    /// <summary>
    /// Starts rotating the turntable to the specified track index.
    /// Stops any ongoing rotation coroutine before starting a new one.
    /// </summary>
    /// <param name="idx">The target track index to rotate to.</param>
    private void StartRotateToIndex(int idx)
    {
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        _rotationCoroutine = StartCoroutine(Co_RotateToIndex(_activeTurntableController, idx));
    }

    /// <summary>
    /// Starts rotating the turntable to the next position in the specified direction.
    /// Stops any ongoing rotation coroutine before starting a new one.
    /// </summary>
    /// <param name="direction">The direction to rotate: 1 for right, -1 for left.</param>
    private void StartRotateToNextPosition(int direction)
    {
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        _rotationCoroutine = StartCoroutine(Co_RotateToNextPosition(_activeTurntableController, direction));
    }

    /// <summary>
    /// Sets the value of the control lever for the turntable.
    /// </summary>
    /// <param name="value">The value to set the control lever to (0.0 to 1.0).</param>
    private void SetControlLever(float value)
    {
        var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(_activeTurntableController);
        kvpObject["controlLever"] = Value.Float(value);
    }


    /// <summary>
    /// Coroutine to rotate the turntable to a specific track index.
    /// Handles ramp-up, steady rotation, slow-down, and fine adjustment phases.
    /// </summary>
    /// <param name="turntableController">The turntable controller to operate on.</param>
    /// <param name="targetIndex">The target track index to rotate to.</param>
    /// <returns>IEnumerator for coroutine execution.</returns>
    ///
    private IEnumerator Co_RotateToIndex(TurntableController? turntableController, int targetIndex)
    {
        if (turntableController == null) yield break;
        var settings = Main.Settings;
        if (settings == null) yield break;

        var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(turntableController);
        var turntable = turntableController.turntable;
        var targetAngle = turntable.AngleForIndex(targetIndex);

        var p = settings.PidSettings.P;
        var i = settings.PidSettings.I;
        var d = settings.PidSettings.D;
        var pid = new PidController(p, i, d);
        pid.Reset();

        while (true)
        {
            float currentAngle = turntable.Angle;
            float error = Mathf.DeltaAngle(currentAngle, targetAngle);

            // Stop if close enough
            if (Mathf.Abs(error) < 0.2f) break;

            float output = pid.Update(targetAngle, currentAngle, Time.deltaTime);

            // Clamp output to [-1, 1] and scale to lever range [0, 1]
            float lever = Mathf.Clamp(output, -1f, 1f);
            kvpObject["controlLever"] = Value.Float(0.5f + lever * 0.5f);

            yield return null;
        }

        kvpObject["controlLever"] = Value.Float(0.5f);
        _rotationCoroutine = null;
    }

    /// <summary>
    /// Flips the turntable 180 degrees from its current angle.
    /// </summary>
    private void FlipTurntable()
    {
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        _rotationCoroutine = StartCoroutine(Co_FlipTurntable(_activeTurntableController));
    }

    /// <summary>
    /// Coroutine to rotate the turntable 180 degrees from its current angle.
    /// </summary>
    private IEnumerator Co_FlipTurntable(TurntableController? turntableController)
    {
        if (turntableController == null) yield break;
        var settings = Main.Settings;
        if (settings == null) yield break;

        var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(turntableController);
        var turntable = turntableController.turntable;
        var currentAngle = turntable.Angle;
        var targetAngle = (currentAngle + 180f) % 360f;

        var p = settings.PidSettings.P;
        var i = settings.PidSettings.I;
        var d = settings.PidSettings.D;
        var pid = new PidController(p, i, d);
        pid.Reset();

        while (true)
        {
            float angleNow = turntable.Angle;
            float error = Mathf.DeltaAngle(angleNow, targetAngle);
            if (Mathf.Abs(error) < 0.2f) break;
            float output = pid.Update(targetAngle, angleNow, Time.deltaTime);
            float lever = Mathf.Clamp(output, -1f, 1f);
            kvpObject["controlLever"] = Value.Float(0.5f + lever * 0.5f);
            yield return null;
        }

        kvpObject["controlLever"] = Value.Float(0.5f);
        _rotationCoroutine = null;
    }

    /// <summary>
    /// Coroutine to rotate the turntable to the next position in the specified direction.
    /// Handles ramp-up, steady rotation, and slow-down phases.
    /// </summary>
    /// <param name="turntableController">The turntable controller to operate on.</param>
    /// <param name="direction">The direction to rotate: 1 for right, -1 for left.</param>
    /// <returns>IEnumerator for coroutine execution.</returns>
    private IEnumerator Co_RotateToNextPosition(TurntableController? turntableController, int direction)
    {
        if (turntableController == null) yield break;
        var settings = Main.Settings;
        if (settings == null) yield break;

        var kvpObject = (KeyValueObject)_keyValueObjectFieldInfo!.GetValue(turntableController);
        var turntable = turntableController.turntable;
        var turntableSubdivisions = turntable.subdivisions;
        var currentIndex = turntable.IndexAndRemainderForAngle(out _);
        var targetIndex = direction > 0
            ? (currentIndex + 1) % turntableSubdivisions
            : (currentIndex - 1 + turntableSubdivisions) % turntableSubdivisions;
        var targetAngle = turntable.AngleForIndex(targetIndex);

        var p = settings.PidSettings.P;
        var i = settings.PidSettings.I;
        var d = settings.PidSettings.D;
        var pid = new PidController(p, i, d);
        pid.Reset();

        while (true)
        {
            float currentAngle = turntable.Angle;
            float error = Mathf.DeltaAngle(currentAngle, targetAngle);

            // Stop if close enough
            if (Mathf.Abs(error) < 0.2f) break;

            float output = pid.Update(targetAngle, currentAngle, Time.deltaTime);

            // Clamp output to [-1, 1] and scale to lever range [0, 1]
            float lever = Mathf.Clamp(output, -1f, 1f);
            kvpObject["controlLever"] = Value.Float(0.5f + lever * 0.5f);

            yield return null;
        }

        kvpObject["controlLever"] = Value.Float(0.5f);
        _rotationCoroutine = null;
    }

    private void JumpToTurntable(TurntableController t)
    {
        if (_mainCamera == null) return;
        _mainCamera.transform.position = t.transform.position + new Vector3(0, 10, 0); // 10 units above
        _mainCamera.transform.LookAt(t.transform);
        Show(t, _allTurntables, _mainCamera); // Switch to control mode for this turntable
    }
}