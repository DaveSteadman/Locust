// <fileheader>

using System;
using System.Numerics;

namespace Locust;

// KoreNumericUtils: A static class for common operations on templated numeric types

public static class KoreNumericUtils
{
    private static readonly System.Random _random = new System.Random();

    // ---------------------------------------------------------------------------------------------

    public static T Min<T>(T a, T b) where T : INumber<T> => a < b ? a : b;
    public static T Max<T>(T a, T b) where T : INumber<T> => a > b ? a : b;

    public static T Abs<T>(T val) where T : INumber<T> => val < T.Zero ? -val : val;

    // ---------------------------------------------------------------------------------------------

    // Usage: T e = KoreNumericUtils.Modulo(5, 3);
    // Usage: T e = KoreNumericUtils.Modulo(5.0f, 3.0f);
    public static T Modulo<T>(T value, T modulus) where T : INumber<T>
    {
        if (modulus == T.Zero)
            throw new ArgumentException("Modulus cannot be zero", nameof(modulus));

        T result = value % modulus;
        if (result < T.Zero)
            result += modulus;

        return result;
    }

    // ---------------------------------------------------------------------------------------------

    // Usage: bool e = KoreNumericUtils.IsInRange(5, 0, 10);
    // Usage: bool e = KoreNumericUtils.IsInRange(5.0f, 0.0f, 10.0f);
    public static bool IsInRange<T>(T val, T rangemin, T rangemax) where T : INumber<T>
    {
        if (rangemin > rangemax) (rangemin, rangemax) = (rangemax, rangemin);
        return val >= rangemin && val <= rangemax;
    }

    // ---------------------------------------------------------------------------------------------

    // Usage: bool e = KoreNumericUtils.EqualsWithinTolerance(5, 5.0001f, 0.001f);
    public static bool EqualsWithinTolerance<T>(T value, T compareVal, T tolerance) where T : INumber<T>
    {
        // Check if the two values are equal within a small tolerance
        return T.Abs(value - compareVal) < tolerance;
    }

    // Usage: bool e = KoreNumericUtils.EqualsWithinTolerance(5.0f, 5.0001f);
    public static bool EqualsWithinTolerance(float a, float b) => EqualsWithinTolerance(a, b, KoreConsts.ArbitrarySmallFloat);
    public static bool EqualsWithinTolerance(double a, double b) => EqualsWithinTolerance(a, b, KoreConsts.ArbitrarySmallDouble);

    public static T ArbitrarySmallValue<T>() where T : INumber<T>
    {
        if (typeof(T) == typeof(float)) return (T)(object)KoreConsts.ArbitrarySmallFloat;
        if (typeof(T) == typeof(double)) return (T)(object)KoreConsts.ArbitrarySmallDouble;
        throw new NotSupportedException($"Type {typeof(T)} is not supported.");
    }

    // ---------------------------------------------------------------------------------------------

    // Usage: T e = KoreNumericUtils.LimitToRange(5, 0, 10);
    public static T LimitToRange<T>(T val, T min, T max) where T : INumber<T>
    {
        if (min > max) (min, max) = (max, min);

        if (val < min) return min;
        if (val > max) return max;
        return val;
    }

    // Usage: T e = KoreNumericUtils.Clamp(5, 0, 10);
    public static T Clamp<T>(T val, T min, T max) where T : INumber<T> => LimitToRange(val, min, max);

    // ---------------------------------------------------------------------------------------------

    // Usage: T e = KoreNumericUtils.WrapToRange(5, 0, 3);
    public static T WrapToRange<T>(T val, T rangemin, T rangemax) where T : INumber<T>
    {
        T diff = rangemax - rangemin;
        T wrappedvalue = Modulo(val - rangemin, diff);
        return wrappedvalue + rangemin;
    }

    public static T WrapToRange<T>(T val, KoreNumericRange<T> range) where T : INumber<T> => WrapToRange(val, range.Min, range.Max);

    // ---------------------------------------------------------------------------------------------

    // Usage: T e = KoreNumericUtils.ScaleToRange(5, 0, 10, 0, 100);
    public static T ScaleToRange<T>(T val, T sourcerangemin, T sourcerangemax, T targetrangemin, T targetrangemax) where T : INumber<T>
    {
        // Flip the min max values to validate the ranges
        if (sourcerangemin > sourcerangemax) (sourcerangemin, sourcerangemax) = (sourcerangemax, sourcerangemin);
        if (targetrangemin > targetrangemax) (targetrangemin, targetrangemax) = (targetrangemax, targetrangemin);

        // Check if the input value is in the source range
        if (!IsInRange(val, sourcerangemin, sourcerangemax))
            throw new ArgumentOutOfRangeException(nameof(val), "Value is outside the source range.");

        // Perform and return the scaling
        T sourceRange = sourcerangemax - sourcerangemin;
        T targetRange = targetrangemax - targetrangemin;

        if (sourceRange < KoreNumericUtils.ArbitrarySmallValue<T>())
            throw new ArgumentException("Source range too small", nameof(sourceRange));
        if (targetRange < KoreNumericUtils.ArbitrarySmallValue<T>())
            throw new ArgumentException("Target range too small", nameof(targetRange));

        return ((val - sourcerangemin) / sourceRange) * targetRange + targetrangemin;
    }

    public static T ScaleToRange<T>(T val, KoreNumericRange<T> sourceRange, KoreNumericRange<T> targetRange) where T : INumber<T>
    {
        return ScaleToRange(val, sourceRange.Min, sourceRange.Max, targetRange.Min, targetRange.Max);
    }

    // ScaleToUncheckedRange: Like ScaleToRange but does not check if val is within source range or if the
    // output range is flipped from the min/max values.
    // Usage: T e = KoreNumericUtils.ScaleToUncheckedRange(5, 0, 10, 200, 100); // Note descending target range
    public static T ScaleToUncheckedRange<T>(T val, T sourcerangemin, T sourcerangemax, T targetrangemin, T targetrangemax) where T : INumber<T>
    {
        // Perform and return the scaling
        T sourceRange = sourcerangemax - sourcerangemin;
        T targetRange = targetrangemax - targetrangemin;

        return ((val - sourcerangemin) / sourceRange) * targetRange + targetrangemin;
    }
    // ---------------------------------------------------------------------------------------------

    // Fraction is the fraction of the max value to use: 0..1 (values beyond this extrapolate)
    // Usage: T e = KoreNumericUtils.Interpolate(0, 10, 0.5f);
    public static T Interpolate<T>(T min, T max, float fraction) where T : INumber<T>
    {
        T t = T.CreateChecked(fraction); // better than Convert.ChangeType
        return min + ((max - min) * t);
    }

    // Lerp naming for the function
    // Usage: T e = KoreNumericUtils.Lerp(0, 10, 0.5f);
    public static T Lerp<T>(T start, T end, float fraction) where T : INumber<T> => Interpolate(start, end, fraction);

    // ---------------------------------------------------------------------------------------------

    // Usage: double val = KoreNumericUtils.RandomInRange(0.0, 10.1);
    // Usage: float  val = KoreNumericUtils.RandomInRange(0.5f, 9.5f);
    public static T RandomInRange<T>(T min, T max) where T : INumber<T>
    {
        if (min > max) (min, max) = (max, min);

        T t = T.CreateChecked(_random.NextDouble()); // generate [0, 1) in T
        return min + (max - min) * t;
    }

    // ---------------------------------------------------------------------------------------------

    // Usage: float  val = KoreNumericUtils.ValuePlusNoise(9.5f, 0.01f);
    public static T ValuePlusNoise<T>(T value, T amplitude) where T : INumber<T>
    {
        T min = value - amplitude;
        T max = value + amplitude;

        return RandomInRange(min, max);
    }

    // ---------------------------------------------------------------------------------------------

    // Usage: T e = KoreNumericUtils.Min3(1, 2, 3);
    public static T Min3<T>(T a, T b, T c) where T : INumber<T>
    {
        T min = a;
        if (b < min) min = b;
        if (c < min) min = c;
        return min;
    }

    // Usage: T e = KoreNumericUtils.Max3(1, 2, 3);
    public static T Max3<T>(T a, T b, T c) where T : INumber<T>
    {
        T max = a;
        if (b > max) max = b;
        if (c > max) max = c;
        return max;
    }

    public static T Mid3<T>(T a, T b, T c) where T : INumber<T>
    {
        if ((a < b && a > c) || (a > b && a < c)) return a;
        if ((b < a && b > c) || (b > a && b < c)) return b;
        return c; // if neither is mid, then c must be mid
    }

}
