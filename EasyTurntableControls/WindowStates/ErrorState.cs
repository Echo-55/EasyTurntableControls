using UI.Builder;

namespace EasyTurntableControls.WindowStates;

public class ErrorState : IWindowState
{
    public string Name => "Error";
    public void OnEnter() { }

    public void OnExit() { }

    public void Build(UIPanelBuilder builder) { }
}