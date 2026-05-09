using System;
using System.Linq;
using Track;
using UI.Builder;
using UnityEngine;

namespace EasyTurntableControls.WindowStates;

public class ControlState(
    Func<TurntableController?> getActiveTurntableController,
    Func<int> getSelectedTrackIndex,
    Action<int> setSelectedTrackIndex,
    Action<int> startRotateToIndex,
    Func<string> getNodesButtonText,
    Action toggleNodesVisualizer,
    Func<float> getControlLever,
    Action<float> setControlLever,
    Action<int> startRotateToNextPosition,
    Action flipTurntable,
    Action openSettings)
    : IWindowState
{
    private const float CenterLeverValue = 0.5f;

    public string Name => "Control";
    public void OnEnter() { }
    public void OnExit() { }

    public void Build(UIPanelBuilder builder)
    {
        var turntableController = getActiveTurntableController();
        if (turntableController == null)
        {
            BuildNoTurntablePanel(builder);
            return;
        }

        BuildControlPanel(builder, turntableController);
    }

    private void BuildNoTurntablePanel(UIPanelBuilder builder)
    {
        builder.AddTitle("Turntable Control Window", "No Turntable Controller");
        BuildTopButtonRow(builder);
        builder.Spacer(10f);
        builder.AddLabel("No turntable controller found in range.");
        builder.Spacer(10f);
    }

    private void BuildControlPanel(UIPanelBuilder builder, TurntableController turntableController)
    {
        builder.AddTitle("Controlling Turntable: ", turntableController.turntable.name);
        BuildTopButtonRow(builder);
        builder.Spacer(10f);

        builder.AddSection("Track Selection", b =>
        {
            var trackCount = turntableController.turntable.subdivisions;
            var trackOptions = Enumerable.Range(0, trackCount).Select(i => $"Track {i}").ToList();
            var currentTrack = turntableController.turntable.StopIndex ?? 0;

            b.AddField("Track", b.AddDropdown(trackOptions, currentTrack, idx =>
            {
                setSelectedTrackIndex(idx);
                startRotateToIndex(idx);
            }));
            b.AddButtonCompact("Go", () => startRotateToIndex(getSelectedTrackIndex()));
        });

        builder.AddButtonCompact(getNodesButtonText, toggleNodesVisualizer);

        builder.Spacer(10f);

        builder.AddSection("Speed Controls", b =>
        {
            b.AddSliderQuantized(
                valueClosure: getControlLever,
                textValueClosure: () => $"{Mathf.Lerp(-100f, 100f, getControlLever()):F0}%",
                valueChangedAction: setControlLever,
                minValue: 0f,
                maxValue: 1f,
                increment: 0.01f,
                editingEndedAction: _ => setControlLever(CenterLeverValue));

            b.HStack(h =>
            {
                h.AddButtonCompact("\u25C4 Rotate Left", () => startRotateToNextPosition(-1));
                h.AddButtonCompact("Rotate Right \u25BA", () => startRotateToNextPosition(1));
            });

            b.AddButtonCompact("Flip 180°", flipTurntable);
            b.Spacer(5f);
            b.AddField("Current Speed", () => $"{Mathf.Lerp(-100f, 100f, getControlLever()):F0}%",
                UIPanelBuilder.Frequency.Fast);
        });

        builder.Spacer(10f);

        builder.AddSection("Status", b =>
        {
            b.AddField("Current Angle", () => $"{turntableController.turntable.Angle:F1}°",
                UIPanelBuilder.Frequency.Fast);
            b.AddField("Current Track", () => $"{turntableController.turntable.StopIndex ?? -1}",
                UIPanelBuilder.Frequency.Fast);
            b.AddField("Status", () => turntableController.turntable.IsLined ? "Lined" : "Not Lined",
                UIPanelBuilder.Frequency.Fast);
        });
    }

    private void BuildTopButtonRow(UIPanelBuilder builder)
    {
        builder.HStack(h =>
        {
            h.Spacer();
            h.AddButtonCompact("⚙ Settings", openSettings);
        });
    }
}