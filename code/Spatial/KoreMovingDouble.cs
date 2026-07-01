// <fileheader>

using System;

//namespace KoreCommon;

// KoreMovingDouble:
// - We have a current value, and a truth value.
// - We increment the current value towards the truth value over a number of steps,
//   to smooth out jumps, likely from maths and messaging update periods.
// - AKA - low-tech smoothing functionality

public class KoreMovingDouble
{
    private readonly object _sync = new();

    public double StartValue        { get; set; }
    public double TargetValue       { get; set; }
    public double StartRuntimeSecs  { get; set; }
    public double ClockDurationSecs { get; set; }

    // --------------------------------------------------------------------------------------------

    public static KoreMovingDouble Zero => new (0d, 0d, 0d);
    public bool IsZero
    {
        get
        {
            lock (_sync)
            {
                return (StartValue < 0.0001d) && (TargetValue < 0.0001d) && (ClockDurationSecs < 0.0001d);
            }
        }
    }

    public void SetZero()
    {
        lock (_sync)
        {
            StartValue        = 0d;
            TargetValue       = 0d;
            StartRuntimeSecs  = 0d;
            ClockDurationSecs = 0d;
        }
    }

    // --------------------------------------------------------------------------------------------

    public KoreMovingDouble(double startval, double targetval, double clockduration)
    {
        StartValue        = startval;
        TargetValue       = targetval;
        StartRuntimeSecs  = KoreCentralTime.RuntimeSecs;
        ClockDurationSecs = clockduration;
    }

    public void Setup(double startval, double targetval, double clockduration)
    {
        lock (_sync)
        {
            StartValue        = startval;
            TargetValue       = targetval;
            StartRuntimeSecs  = KoreCentralTime.RuntimeSecs;
            ClockDurationSecs = clockduration;
        }
    }

    // Instantly forces the CurrentValue to the CommandedValue
    public void ForceToValue(double commandedValue)
    {
        lock (_sync)
        {
            StartValue        = commandedValue;
            TargetValue       = commandedValue;
            StartRuntimeSecs  = KoreCentralTime.RuntimeSecs;
            ClockDurationSecs = 0d;
        }
    }

    public double CurrentValue
    {
        get
        {
            lock (_sync)
            {
                double elapsedSecs = KoreCentralTime.RuntimeSecs - StartRuntimeSecs;
                if (elapsedSecs >= ClockDurationSecs)
                {
                    StartValue        = TargetValue;
                    StartRuntimeSecs  = KoreCentralTime.RuntimeSecs;
                    ClockDurationSecs = 0d;
                    return TargetValue;
                }

                double fraction = elapsedSecs / ClockDurationSecs;
                return StartValue + (TargetValue - StartValue) * fraction;
            }
        }
    }
}


