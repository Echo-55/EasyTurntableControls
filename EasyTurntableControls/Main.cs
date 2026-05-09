using UnityEngine;
using UnityModManagerNet;
using Object = UnityEngine.Object;

namespace EasyTurntableControls
{
#if DEBUG
    [EnableReloading]
#endif
    public class Main
    {
        private static UnityModManager.ModEntry? _modEntry;
        private static GameObject? _hookObject;

        public static bool IsEnabled => _modEntry?.Enabled ?? false;
        public static EasyTurntableControlsSettings? Settings;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            DetachCallbacks(modEntry);
            _modEntry = modEntry;
            Settings = UnityModManager.ModSettings.Load<EasyTurntableControlsSettings>(modEntry);
            _modEntry.OnToggle += OnToggle;
            _modEntry.OnGUI += OnGUI;
            _modEntry.OnSaveGUI += OnSaveGUI;

            if (modEntry.Enabled)
                OnEnable();

            modEntry.Logger.Log("EasyTurntableControls loaded");
            return true;
        }

        public static bool Unload(UnityModManager.ModEntry modEntry)
        {
            SaveSettings();
            OnDisable();
            DetachCallbacks(modEntry);
            Settings = null;

            if (_modEntry == modEntry)
                _modEntry = null;

            modEntry.Logger.Log("EasyTurntableControls unloaded");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool isModEnabled)
        {
            if (isModEnabled)
                OnEnable();
            else
                OnDisable();

            return true;
        }

        private static void OnEnable()
        {
            if (_hookObject != null) return;
            _hookObject = new GameObject("EasyTurntableControls");
            _hookObject.AddComponent<EasyTurntableControlsController>();
            Object.DontDestroyOnLoad(_hookObject);
            _modEntry?.Logger.Log("EasyTurntableControls enabled");
        }

        private static void OnDisable()
        {
            if (_hookObject == null) return;
            Object.Destroy(_hookObject);
            _hookObject = null;
            _modEntry?.Logger.Log("EasyTurntableControls disabled");
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry) => Settings?.Draw(modEntry);

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry) => Settings?.Save(modEntry);

        public static void SaveSettings()
        {
            if (_modEntry != null && Settings != null)
                Settings.Save(_modEntry);
        }

        private static void DetachCallbacks(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnToggle -= OnToggle;
            modEntry.OnGUI -= OnGUI;
            modEntry.OnSaveGUI -= OnSaveGUI;
        }

        [DrawFields(DrawFieldMask.Public)]
        public class EasyTurntableControlsSettings : UnityModManager.ModSettings, IDrawable
        {
            // ReSharper disable once FieldCanBeMadeReadOnly.Global
            public KeyBinding ToggleTurnTableControlWindow = new() { keyCode = KeyCode.Y };
            public float DistanceForTurntableSearch = 250f;
            public PidControllerSettings PidSettings = new();

            public void ResetWindowEditableSettings()
            {
                DistanceForTurntableSearch = 250f;
                PidSettings = new PidControllerSettings();
            }

            public void OnChange()
            {
                // noop
            }
        }
    }
}