using EasyTurntableControls.WindowStates;
using UI.Builder;

namespace EasyTurntableControls;

public class StateMachine(StateMachine.SettingsTuning? settingsTuning = null)
{
    private IWindowState? _currentState;
    private IWindowState? _previousState;

    public IWindowState? CurrentState => _currentState;
    public IWindowState? PreviousState => _previousState;
    public SettingsTuning Settings { get; } = settingsTuning ?? new SettingsTuning();

    public void TransitionTo(IWindowState newState)
    {
        if (_currentState == newState)
            return;

        _currentState?.OnExit();
        _previousState = _currentState;
        _currentState = newState;
        _currentState.OnEnter();
    }

    public void ClearCurrent()
    {
        if (_currentState == null)
            return;

        _currentState.OnExit();
        _previousState = _currentState;
        _currentState = null;
    }

    public void BuildCurrentState(UIPanelBuilder builder) => _currentState?.Build(builder);

    public sealed class SettingsTuning(
        float searchDistanceIncrement = 10f,
        float searchDistanceMin = 50f,
        float searchDistanceMax = 1000f,
        float pidPIncrement = 0.005f,
        float pidPMax = 1f,
        float pidIIncrement = 0.001f,
        float pidIMax = 0.25f,
        float pidDIncrement = 0.001f,
        float pidDMax = 0.5f)
    {
        public float SearchDistanceIncrement { get; } = searchDistanceIncrement;
        public float SearchDistanceMin { get; } = searchDistanceMin;
        public float SearchDistanceMax { get; } = searchDistanceMax;
        public float PidPIncrement { get; } = pidPIncrement;
        public float PidPMax { get; } = pidPMax;
        public float PidIIncrement { get; } = pidIIncrement;
        public float PidIMax { get; } = pidIMax;
        public float PidDIncrement { get; } = pidDIncrement;
        public float PidDMax { get; } = pidDMax;
    }
}