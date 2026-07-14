// <fileheader>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Locust;

public static partial class KoreNumeric1DArrayOps<T> where T : struct, INumber<T>
{
    // --------------------------------------------------------------------------------------------
    // MARK: Bezier functions
    // --------------------------------------------------------------------------------------------

    public static T CalculateBezierPoint(T t, KoreNumeric1DArray<T> controlPoints)
    {
        switch (controlPoints.Length)
        {
            case 3:
                return CalculateBezier3Point(t, controlPoints);
            case 4:
                return CalculateBezier4Point(t, controlPoints);
            case 5:
                return CalculateBezier5Point(t, controlPoints);
            default:
                throw new InvalidOperationException("Unsupported number of control points.");
        }
    }

    public static KoreNumeric1DArray<T> CalculateBezierFirstDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        switch (controlPoints.Length)
        {
            case 3:
                return CalculateBezier3PointFirstDerivative(t, controlPoints);
            case 4:
                return CalculateBezier4PointFirstDerivative(t, controlPoints);
            case 5:
                return CalculateBezier5PointFirstDerivative(t, controlPoints);
            default:
                throw new InvalidOperationException("Unsupported number of control points.");
        }
    }

    public static KoreNumeric1DArray<T> CalculateBezierSecondDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        switch (controlPoints.Length)
        {
            case 3:
                return CalculateBezier3PointSecondDerivative(controlPoints);
            case 4:
                return CalculateBezier4PointSecondDerivative(t, controlPoints);
            case 5:
                return CalculateBezier5PointSecondDerivative(t, controlPoints);
            default:
                throw new InvalidOperationException("Unsupported number of control points.");
        }
    }

    // --------------------------------------------------------------------------------------------
    // MARK: 3 Point Bezier
    // --------------------------------------------------------------------------------------------

    private static T CalculateBezier3Point(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T u = T.One - t;
        T tt = t * t;
        T uu = u * u;

        return uu * controlPoints[0] + T.CreateChecked(2) * u * t * controlPoints[1] + tt * controlPoints[2];
    }

    private static KoreNumeric1DArray<T> CalculateBezier3PointFirstDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T dx = T.CreateChecked(2) * (T.One - t) * (controlPoints[1] - controlPoints[0]) +
                T.CreateChecked(2) * t * (controlPoints[2] - controlPoints[1]);

        return new KoreNumeric1DArray<T>(new T[] { dx });
    }

    private static KoreNumeric1DArray<T> CalculateBezier3PointSecondDerivative(KoreNumeric1DArray<T> controlPoints)
    {
        T dx = T.CreateChecked(2) * (controlPoints[2] - T.CreateChecked(2) * controlPoints[1] + controlPoints[0]);

        return new KoreNumeric1DArray<T>(new T[] { dx });
    }

    // --------------------------------------------------------------------------------------------
    // MARK: 4 Point Bezier
    // --------------------------------------------------------------------------------------------

    private static T CalculateBezier4Point(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T u = T.One - t;
        T tt = t * t;
        T uu = u * u;
        T ttt = tt * t;
        T uuu = uu * u;

        return uuu * controlPoints[0] +
               T.CreateChecked(3) * uu * t * controlPoints[1] +
               T.CreateChecked(3) * u * tt * controlPoints[2] +
               ttt * controlPoints[3];
    }

    private static KoreNumeric1DArray<T> CalculateBezier4PointFirstDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T dx = T.CreateChecked(3) * (T.One - t) * (T.One - t) * (controlPoints[1] - controlPoints[0]) +
                T.CreateChecked(6) * (T.One - t) * t * (controlPoints[2] - controlPoints[1]) +
                T.CreateChecked(3) * t * t * (controlPoints[3] - controlPoints[2]);

        return new KoreNumeric1DArray<T>(new T[] { dx });
    }

    private static KoreNumeric1DArray<T> CalculateBezier4PointSecondDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T dx = T.CreateChecked(6) * (T.One - t) * (controlPoints[2] - T.CreateChecked(2) * controlPoints[1] + controlPoints[0]) +
                T.CreateChecked(6) * t * (controlPoints[3] - T.CreateChecked(2) * controlPoints[2] + controlPoints[1]);

        return new KoreNumeric1DArray<T>(new T[] { dx });
    }

    // --------------------------------------------------------------------------------------------
    // MARK: 5 Point Bezier
    // --------------------------------------------------------------------------------------------

    private static T CalculateBezier5Point(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T u = T.One - t;
        T tt = t * t;
        T uu = u * u;
        T ttt = tt * t;
        T uuu = uu * u;
        T tttt = ttt * t;
        T uuuu = uuu * u;

        return uuuu * controlPoints[0] +
               T.CreateChecked(4) * uuu * t * controlPoints[1] +
               T.CreateChecked(6) * uu * tt * controlPoints[2] +
               T.CreateChecked(4) * u * ttt * controlPoints[3] +
               tttt * controlPoints[4];
    }

    private static KoreNumeric1DArray<T> CalculateBezier5PointFirstDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T dx = T.CreateChecked(4) * (T.One - t) * (T.One - t) * (T.One - t) * (controlPoints[1] - controlPoints[0]) +
                T.CreateChecked(12) * (T.One - t) * (T.One - t) * t * (controlPoints[2] - controlPoints[1]) +
                T.CreateChecked(12) * (T.One - t) * t * t * (controlPoints[3] - controlPoints[2]) +
                T.CreateChecked(4) * t * t * t * (controlPoints[4] - controlPoints[3]);

        return new KoreNumeric1DArray<T>(new T[] { dx });
    }

    private static KoreNumeric1DArray<T> CalculateBezier5PointSecondDerivative(T t, KoreNumeric1DArray<T> controlPoints)
    {
        T dx = T.CreateChecked(12) * (T.One - t) * (controlPoints[2] - T.CreateChecked(2) * controlPoints[1] + controlPoints[0]) +
                T.CreateChecked(24) * (T.One - t) * t * (controlPoints[3] - T.CreateChecked(2) * controlPoints[2] + controlPoints[1]) +
                T.CreateChecked(12) * t * t * (controlPoints[4] - T.CreateChecked(2) * controlPoints[3] + controlPoints[2]);

        return new KoreNumeric1DArray<T>(new T[] { dx });
    }
}
