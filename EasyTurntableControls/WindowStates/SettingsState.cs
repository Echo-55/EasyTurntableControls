using UI.Builder;

namespace EasyTurntableControls.WindowStates;

public class SettingsState(
    StateMachine stateMachine,
    System.Func<Main.EasyTurntableControlsSettings?> getSettings,
    System.Action<System.Action<Main.EasyTurntableControlsSettings>> updateSettings,
    System.Action closeSettings,
    System.Action resetEditableSettings,
    System.Action? onExit = null)
    : IWindowState
{
    public string Name => "Settings";

    public void OnEnter() { }

    public void OnExit() { onExit?.Invoke(); }

    public void Build(UIPanelBuilder builder)
    {
        builder.AddTitle("Turntable Control Window", "Settings");
        builder.HStack(h =>
        {
            h.AddButtonCompact("← Back", closeSettings);
            h.Spacer();
            h.AddButtonCompact("Reset Defaults", resetEditableSettings);
        });
        builder.Spacer(10f);

        var settings = getSettings();
        if (settings == null)
        {
            builder.AddLabel("Settings are unavailable right now.");
            return;
        }

        builder.AddLabel("Changes apply immediately and are saved when you leave settings or close the window.",
            configure => configure.fontSize = 12);
        builder.Spacer(10f);

        builder.AddSection("Turntable Search", b =>
        {
            b.AddField("Search Radius",
                b.AddSliderQuantized(
                    valueClosure: () => settings.DistanceForTurntableSearch,
                    textValueClosure: () => $"{settings.DistanceForTurntableSearch:F0} m",
                    valueChangedAction: value => updateSettings(s => s.DistanceForTurntableSearch = value),
                    increment: stateMachine.Settings.SearchDistanceIncrement,
                    minValue: stateMachine.Settings.SearchDistanceMin,
                    maxValue: stateMachine.Settings.SearchDistanceMax));
        });

        builder.Spacer(10f);

        builder.AddSection("PID Tuning", b =>
        {
            b.AddField("P",
                b.AddSliderQuantized(
                    valueClosure: () => settings.PidSettings.P,
                    textValueClosure: () => settings.PidSettings.P.ToString("F3"),
                    valueChangedAction: value => updateSettings(s => s.PidSettings.P = value),
                    increment: stateMachine.Settings.PidPIncrement,
                    minValue: 0f,
                    maxValue: stateMachine.Settings.PidPMax));

            b.AddField("I",
                b.AddSliderQuantized(
                    valueClosure: () => settings.PidSettings.I,
                    textValueClosure: () => settings.PidSettings.I.ToString("F3"),
                    valueChangedAction: value => updateSettings(s => s.PidSettings.I = value),
                    increment: stateMachine.Settings.PidIIncrement,
                    minValue: 0f,
                    maxValue: stateMachine.Settings.PidIMax));

            b.AddField("D",
                b.AddSliderQuantized(
                    valueClosure: () => settings.PidSettings.D,
                    textValueClosure: () => settings.PidSettings.D.ToString("F3"),
                    valueChangedAction: value => updateSettings(s => s.PidSettings.D = value),
                    increment: stateMachine.Settings.PidDIncrement,
                    minValue: 0f,
                    maxValue: stateMachine.Settings.PidDMax));
        });

        builder.Spacer(10f);
        builder.AddSection("Notes", b =>
        {
            b.AddLabel("The window hotkey is still configured in Unity Mod Manager.",
                configure => configure.fontSize = 12);
        });
    }
}