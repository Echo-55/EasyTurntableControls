using UI.Builder;

namespace EasyTurntableControls.WindowStates;

public interface IWindowState
{
    string Name { get; }
    void OnEnter();
    void OnExit();
    void Build(UIPanelBuilder builder);
}