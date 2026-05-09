using System;
using System.Collections.Generic;
using Track;
using UI.Builder;
using UnityEngine;

namespace EasyTurntableControls.WindowStates;

public class SearchState(
    Func<List<TurntableController>?> getAllTurntables,
    Func<Camera?> getMainCamera,
    Action openSettings,
    Action<TurntableController> jumpToTurntable)
    : IWindowState
{
    public string Name => "Search";

    public void OnEnter() { }

    public void OnExit() { }

    public void Build(UIPanelBuilder builder)
    {
        builder.AddTitle("Turntable Selection", "");
        builder.HStack(h =>
        {
            h.Spacer();
            h.AddButtonCompact("⚙ Settings", openSettings);
        });
        builder.Spacer(10f);

        var allTurntables = getAllTurntables();
        if (allTurntables == null || allTurntables.Count == 0)
        {
            builder.AddLabel("No turntables found in the world.");
            return;
        }

        builder.AddLabel("No turntable in range. Select one to jump to:");
        builder.Spacer(5f);
        builder.HScrollView(b =>
        {
            var camera = getMainCamera();
            foreach (TurntableController turntableController in allTurntables)
            {
                if (turntableController == null)
                    continue;

                var turntableName = turntableController.name;
                var distance = camera != null
                    ? Vector3.Distance(camera.transform.position, turntableController.transform.position)
                    : 0f;

                b.HStack(h =>
                {
                    h.AddLabel($"{turntableName} ({distance:F1}m)");
                    h.AddButtonCompact("Jump to", () => jumpToTurntable(turntableController));
                });
            }
        }, new RectOffset(5, 5, 5, 5));
    }
}