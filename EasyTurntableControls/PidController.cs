namespace EasyTurntableControls;

public class PidController(float kp, float ki, float kd)
{
    private readonly float Kp = kp, Ki = ki, Kd = kd;
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