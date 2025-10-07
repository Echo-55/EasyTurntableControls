using System;
using UnityEngine;
using UnityModManagerNet;
using Object = UnityEngine.Object;

namespace EasyTurntableControls
{
    public class Main
    {
        private static UnityModManager.ModEntry? _modEntry;
        private static GameObject? _hookObject;

        public static bool IsEnabled => _modEntry?.Enabled ?? false;
        public static EasyTurntableControlsSettings? Settings;

        public static void Load(UnityModManager.ModEntry modEntry)
        {
            _modEntry = modEntry;
            _modEntry.OnToggle += ModEntry_OnToggle;
            _modEntry.OnGUI += ModEntry_OnGUI;
            _modEntry.OnSaveGUI += ModEntry_OnSaveGUI;
            Settings = UnityModManager.ModSettings.Load<EasyTurntableControlsSettings>(modEntry);
        }

        private static bool ModEntry_OnToggle(UnityModManager.ModEntry modEntry, bool isModEnabled)
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
        }

        private static void OnDisable()
        {
            if (_hookObject == null) return;
            Object.Destroy(_hookObject);
            _hookObject = null;
        }

        private static void ModEntry_OnGUI(UnityModManager.ModEntry modEntry) { Settings?.Draw(modEntry); }

        private static void ModEntry_OnSaveGUI(UnityModManager.ModEntry obj) { Settings?.Save(obj); }

        [DrawFields(DrawFieldMask.Public)]
        public class EasyTurntableControlsSettings : UnityModManager.ModSettings, IDrawable
        {
            public KeyBinding ToggleTurnTableControlWindow = new() { keyCode = KeyCode.Y };
            public float Speed = 1f; // how fast the turntable rotates
            public float RampTime = 0.2f; // time to reach full speed
            public float SlowDownAngle = 1f; // angle at which to start slowing down
            public float DistanceForTurntableSearch = 250f;
            public PidControllerSettings PidSettings = new();

            public void OnChange()
            {
                // noop
            }

            [Serializable]
            public struct PidControllerSettings
            {
                public float P = 0.05f;
                public float I = 0f;
                public float D = 0.01f;

                public PidControllerSettings() { }
            }
        }
    }
}