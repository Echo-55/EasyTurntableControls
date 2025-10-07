using System.Collections.Generic;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using Helpers;
using Serilog;
using Track;
using UI;
using UI.Builder;
using UI.Common;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace EasyTurntableControls;

public class EasyTurntableControlsController : MonoBehaviour
{
    private ILogger _logger = Log.ForContext<EasyTurntableControlsController>();
    private static EasyTurntableControlsController? _instance;

    private ProgrammaticWindowCreator? _programmaticWindowCreator;
    private List<TurntableController> _turntableControllersList = new();
    private Camera? _mainCamera;
    private WindowManager? _windowManager;
    private EasyTurntableControlsWindow? _turntableControlWindow;
    public EasyTurntableControlsController? Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        Messenger.Default.Register<MapDidLoadEvent>(this, OnMapLoaded);
        Messenger.Default.Register<MapDidUnloadEvent>(this, OnMapUnLoaded);
    }

    private void OnDisable()
    {
        Messenger.Default.Unregister<MapDidLoadEvent>(this);
        Messenger.Default.Unregister<MapDidUnloadEvent>(this);
        OnWorldUnloaded();
    }

    private void OnMapLoaded(MapDidLoadEvent obj) { OnWorldLoaded(); }

    private void OnMapUnLoaded(MapDidUnloadEvent obj) { OnWorldUnloaded(); }

    private void Update()
    {
        if (Main.Settings == null) return;
        if (Main.Settings.ToggleTurnTableControlWindow.Down())
            ToggleWindow();
    }


    public void OnWorldLoaded()
    {
        _programmaticWindowCreator = FindObjectOfType<ProgrammaticWindowCreator>(true);
        if (_programmaticWindowCreator == null)
        {
            _logger.Error("ProgrammaticWindowCreator not found");
            return;
        }

        Window? windowPrefab = _programmaticWindowCreator.windowPrefab;
        if (windowPrefab == null)
        {
            _logger.Error("Window prefab is null in ProgrammaticWindowCreator");
            return;
        }

        UIBuilderAssets? builderAssets = _programmaticWindowCreator.builderAssets;
        if (builderAssets == null)
        {
            _logger.Error("BuilderAssets is null in ProgrammaticWindowCreator");
            return;
        }

        _windowManager = WindowManager.Shared;
        if (_windowManager == null)
        {
            _logger.Error("WindowManager not found");
            return;
        }

        _turntableControllersList = FindObjectsOfType<TurntableController>().ToList();

        _turntableControlWindow = CreateWindow<EasyTurntableControlsWindow>(windowPrefab, builderAssets);

        if (_turntableControlWindow == null)
        {
            _logger.Error("Failed to create New Turntable Control Window");
        }

        _turntableControlWindow?.OnWorldLoaded();
    }


    public void OnWorldUnloaded()
    {
        _instance = null;
        _programmaticWindowCreator = null;
        _turntableControlWindow?.OnWorldUnloaded();
        _turntableControlWindow = null;
    }

    private void ToggleWindow()
    {
        if (_turntableControlWindow == null)
        {
            _logger.Error("Turntable Control Window is null when trying to toggle it.");
            return;
        }

        if (_turntableControlWindow.IsShown)
        {
            _turntableControlWindow.Hide();
            return;
        }

        if (!MainCameraHelper.TryGetIfNeeded(ref _mainCamera))
        {
            _logger.Information("Failed to get main camera");
            return;
        }

        TurntableController? turnTableController = null;
        foreach (TurntableController? controller in _turntableControllersList)
        {
            if (controller == null) continue;
            var distance = Vector3.Distance(_mainCamera.transform.position, controller.transform.position);
            if (distance <= Main.Settings!.DistanceForTurntableSearch)
            {
                turnTableController = controller;
                _logger.Information($"Found TurntableController: {controller.name} at distance {distance:F2}");
                break;
            }
        }

        if (turnTableController == null)
        {
            // No turntable in range: show list mode
            _turntableControlWindow.Show(null, _turntableControllersList, _mainCamera);
            return;
        }

        // In range: show control mode
        _turntableControlWindow.Show(turnTableController, _turntableControllersList, _mainCamera);
    }

    private TWindow? CreateWindow<TWindow>(Window windowPrefab, UIBuilderAssets builderAssets)
        where TWindow : Component, IProgrammaticWindow
    {
        _windowManager ??= WindowManager.Shared;
        if (_windowManager == null)
        {
            _logger.Error("WindowManager not found");
            return null;
        }

        Window window = Instantiate(windowPrefab, _windowManager.transform, true);
        window.name = typeof(TWindow).ToString();
        var windowComponent = window.gameObject.AddComponent<TWindow>();
        windowComponent.BuilderAssets = builderAssets;
        window.CloseWindow();
        window.SetInitialPositionSize(windowComponent.WindowIdentifier, windowComponent.DefaultSize,
            windowComponent.DefaultPosition,
            windowComponent.Sizing);

        return windowComponent;
    }
}