using System;

namespace EasyTurntableControls;

// P: reacts to how far off the turntable is right now
// I: corrects small leftover error over time
// D: smooths movement and reduces overshoot

public class PidController(PidControllerSettings pidControllerSettings)
{
    private readonly float Kp = pidControllerSettings.P, Ki = pidControllerSettings.I, Kd = pidControllerSettings.D;
    private float _integral, _lastError;

    public float Update(float target, float actual, float deltaTime)
    {
        var error = target - actual;
        _integral += error * deltaTime;
        var derivative = (error - _lastError) / deltaTime;
        _lastError = error;
        return Kp * error + Ki * _integral + Kd * derivative;
    }

    public void Reset()
    {
        _integral = 0;
        _lastError = 0;
    }
}

[Serializable]
public struct PidControllerSettings
{
    public float P = 0.05f;
    public float I = 0f;
    public float D = 0.01f;

    public PidControllerSettings() { }
}