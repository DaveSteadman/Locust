namespace Locust;

public struct QuadTreePayload
{
    // --------------------------------------------------------------------------------------------
    // MARK: State
    // --------------------------------------------------------------------------------------------

    public KoreMovingDouble Value { get; private set; }

    // --------------------------------------------------------------------------------------------
    // MARK: Operations
    // --------------------------------------------------------------------------------------------

    public void SetValue(double value, double decaySecs)
    {
        Value = new KoreMovingDouble(value, 0d, decaySecs);
    }

    public double GetCurrentValue()
    {
        return Value?.CurrentValue ?? 0d;
    }

    public double GetValueForTime(double runtimeSecs)
    {
        return Value?.GetValueForTime(runtimeSecs) ?? 0d;
    }
}
