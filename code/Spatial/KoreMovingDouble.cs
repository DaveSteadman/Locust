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
    public double StartValue        { get; set; }
    public double TargetValue       { get; set; }
    public double StartRuntimeSecs  { get; set; }
    public double ClockDurationSecs { get; set; }

    // --------------------------------------------------------------------------------------------

    public static KoreMovingDouble Zero => new (0d, 0d, 0d);
    public bool IsZero => (StartValue < 0.0001d) && (TargetValue < 0.0001d) && (ClockDurationSecs < 0.0001d);
    public void SetZero()
    {
        StartValue        = 0d;
        TargetValue       = 0d;
        StartRuntimeSecs  = 0d;
        ClockDurationSecs = 0d;
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
        StartValue        = startval;
        TargetValue       = targetval;
        StartRuntimeSecs  = KoreCentralTime.RuntimeSecs;
        ClockDurationSecs = clockduration;
    }

    // Instantly forces the CurrentValue to the CommandedValue
    public void ForceToValue(double commandedValue)
    {
        StartValue  = commandedValue;
        TargetValue = commandedValue;
        StartRuntimeSecs  = KoreCentralTime.RuntimeSecs;
        ClockDurationSecs = 0d;
    }

    public double CurrentValue
    {
        get
        {
            double elapsedSecs = KoreCentralTime.RuntimeSecs - StartRuntimeSecs;
            if (elapsedSecs >= ClockDurationSecs)
            {
                ForceToValue(TargetValue);
                return TargetValue;
            }
            else
            {
                double fraction = elapsedSecs / ClockDurationSecs;
                return StartValue + (TargetValue - StartValue) * fraction;
            }
        }
    }
}


