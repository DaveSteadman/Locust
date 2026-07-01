// <fileheader>

using System;
using System.Numerics;

namespace Locust.Spatial;

/*
Usage:
    KoreConsts<float>.kPi
    KoreConsts<T>.kPi

*/

public static class KoreConsts<T> where T : INumber<T>
{
    public static readonly T kPi;
    public static readonly T kTwoPi;
    //public static readonly T DegsInRads90;
    public static readonly T RadsToDegsMultiplier;
    public static readonly T DegsToRadsMultiplier;
    public static readonly T ArbitraryMin;
    public readonly static T kHalfPi;

    static KoreConsts()
    {
        if (typeof(T) == typeof(double))
        {
            kPi = (T)(object)Math.PI;
            kTwoPi = (T)(object)(2 * Math.PI);
            kHalfPi = (T)(object)(Math.PI / 2);
            RadsToDegsMultiplier = (T)(object)(180.0 / Math.PI);
            DegsToRadsMultiplier = (T)(object)(Math.PI / 180.0);
            ArbitraryMin = (T)(object)0.00001;
        }
        else if (typeof(T) == typeof(float))
        {
            // For float you can use MathF in .NET Core 2.1+.
            // If MathF is not available, you can just cast double to float.
            kPi = (T)(object)MathF.PI;
            kTwoPi = (T)(object)(2f * MathF.PI);
            kHalfPi = (T)(object)(MathF.PI / 2);
            RadsToDegsMultiplier = (T)(object)(180f / MathF.PI);
            DegsToRadsMultiplier = (T)(object)(MathF.PI / 180f);
            ArbitraryMin = (T)(object)0.00001f;
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T)} is not supported.");
        }
    }
}
