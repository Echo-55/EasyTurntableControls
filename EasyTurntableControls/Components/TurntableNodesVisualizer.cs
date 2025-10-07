using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Track;
using UnityEngine;

namespace EasyTurntableControls.Components;

public class TurntableNodesVisualizer : MonoBehaviour
{
    private TurntableController? _controller;
    private FieldInfo? _turntableNodesFieldInfo;

    private List<TrackNodeVisual> _trackNodeVisuals = new();

    private void Awake() { _turntableNodesFieldInfo = AccessTools.Field(typeof(Turntable), "nodes"); }

    public void Show(TurntableController turntableController)
    {
        _controller = turntableController;
        if (_controller == null || _turntableNodesFieldInfo == null) return;

        var turntable = turntableController.turntable;
        var nodes = (List<TrackNode>)_turntableNodesFieldInfo.GetValue(turntable);
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            var go = new GameObject($"NodeVisual_{node.id}");
            go.transform.SetParent(transform);
            go.transform.position = node.transform.position;
            var visual = go.AddComponent<TrackNodeVisual>();
            visual.Init(node);
            _trackNodeVisuals.Add(visual);
        }
    }

    public void Hide()
    {
        foreach (var visual in _trackNodeVisuals) Destroy(visual.gameObject);
        _trackNodeVisuals.Clear();
        _controller = null;
    }
}