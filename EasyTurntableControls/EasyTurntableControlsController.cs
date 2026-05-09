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
    private readonly ILogger _logger = Log.ForContext<EasyTurntableControlsController>();
    private static EasyTurntableControlsController? _instance;

    private ProgrammaticWindowCreator? _programmaticWindowCreator;
    private List<TurntableController> _turntableControllersList = new();
    private Camera? _mainCamera;
    private WindowManager? _windowManager;
    private EasyTurntableControlsWindow? _turntableControlWindow;

    public static EasyTurntableControlsController? Instance => _instance;

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
        Messenger.Default.Register<MapDidLoadEvent>(this, _ => OnWorldLoaded());
        Messenger.Default.Register<MapDidUnloadEvent>(this, _ => OnWorldUnloaded());
        TryAttachToCurrentWorld();
    }

    private void Start()
    {
        TryAttachToCurrentWorld();
    }

    private void OnDisable()
    {
        Messenger.Default.Unregister<MapDidLoadEvent>(this);
        Messenger.Default.Unregister<MapDidUnloadEvent>(this);
        DetachFromWorld();

        if (_instance == this)
            _instance = null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (Main.Settings?.ToggleTurnTableControlWindow.Down() == true)
            ToggleWindow();
    }

    public void OnWorldLoaded() => AttachToWorld(forceRecreate: true, logFailure: true);

    public void OnWorldUnloaded() => DetachFromWorld();

    private void TryAttachToCurrentWorld() => AttachToWorld(forceRecreate: false, logFailure: false);

    private void AttachToWorld(bool forceRecreate, bool logFailure)
    {
        if (forceRecreate)
            DetachFromWorld();
        else if (_turntableControlWindow != null)
            return;

        _programmaticWindowCreator = FindObjectOfType<ProgrammaticWindowCreator>(true);
        if (_programmaticWindowCreator == null)
        {
            if (logFailure)
                _logger.Error("ProgrammaticWindowCreator not found");
            return;
        }

        Window? windowPrefab = _programmaticWindowCreator.windowPrefab;
        if (windowPrefab == null)
        {
            if (logFailure)
                _logger.Error("Window prefab is null in ProgrammaticWindowCreator");
            return;
        }

        UIBuilderAssets? builderAssets = _programmaticWindowCreator.builderAssets;
        if (builderAssets == null)
        {
            if (logFailure)
                _logger.Error("BuilderAssets is null in ProgrammaticWindowCreator");
            return;
        }

        _windowManager = WindowManager.Shared;
        if (_windowManager == null)
        {
            if (logFailure)
                _logger.Error("WindowManager not found");
            return;
        }

        _turntableControllersList = FindObjectsOfType<TurntableController>().ToList();

        _turntableControlWindow = CreateWindow<EasyTurntableControlsWindow>(windowPrefab, builderAssets);

        if (_turntableControlWindow == null)
        {
            _logger.Error("Failed to create turntable control window");
            return;
        }

        _turntableControlWindow.OnWorldLoaded();
    }

    private void DetachFromWorld()
    {
        _mainCamera = null;

        if (_turntableControlWindow != null)
        {
            _turntableControlWindow.Cleanup();
            Destroy(_turntableControlWindow.gameObject);
            _turntableControlWindow = null;
        }

        _programmaticWindowCreator = null;
        _windowManager = null;
        _turntableControllersList.Clear();
    }

    private void ToggleWindow()
    {
        if (_turntableControlWindow == null)
        {
            TryAttachToCurrentWorld();
            if (_turntableControlWindow == null)
            {
                _logger.Error("Turntable control window is null when trying to toggle it.");
                return;
            }
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

        TurntableController? nearestController = FindNearestTurntable(_mainCamera!.transform.position);
        _turntableControlWindow.Show(nearestController, _turntableControllersList, _mainCamera);
    }

    private TurntableController? FindNearestTurntable(Vector3 position)
    {
        float searchDistance = Main.Settings!.DistanceForTurntableSearch;
        foreach (TurntableController controller in _turntableControllersList)
        {
            if (controller == null) continue;
            float distance = Vector3.Distance(position, controller.transform.position);
            if (distance <= searchDistance)
            {
                _logger.Information("Found TurntableController: {Name} at distance {Distance:F2}", controller.name, distance);
                return controller;
            }
        }

        return null;
    }

    private TWindow CreateWindow<TWindow>(Window windowPrefab, UIBuilderAssets builderAssets)
        where TWindow : Component, IProgrammaticWindow
    {
        Window window = Instantiate(windowPrefab, _windowManager!.transform, true);
        window.name = typeof(TWindow).Name;
        var windowComponent = window.gameObject.AddComponent<TWindow>();
        windowComponent.BuilderAssets = builderAssets;
        window.CloseWindow();
        window.SetInitialPositionSize(
            windowComponent.WindowIdentifier,
            windowComponent.DefaultSize,
            windowComponent.DefaultPosition,
            windowComponent.Sizing);

        return windowComponent;
    }
}