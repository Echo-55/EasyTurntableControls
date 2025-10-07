using System.Text.RegularExpressions;
using Track;
using UnityEngine;

namespace EasyTurntableControls.Components;

public class TrackNodeVisual : MonoBehaviour
{
    public TrackNode? Node;
    private Camera? _camera;

    private void Awake() { _camera = Camera.main; }

    public void Init(TrackNode? node) { Node = node; }

    private void OnGUI()
    {
        if (Node == null || _camera == null) return;
        var screenPos = _camera.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0)
        {
            var size = 50;
            var rect = new Rect(screenPos.x - size / 2, Screen.height - screenPos.y - size / 2, size, size);
            GUI.color = Color.red;
            var id = GetTrailingDigits(Node.id);
            GUI.Label(rect, id);
        }
    }

    private string GetTrailingDigits(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        var match = Regex.Match(id, @"(\d+)$");
        return match.Success ? match.Value : string.Empty;
    }
}