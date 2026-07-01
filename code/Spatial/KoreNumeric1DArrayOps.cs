// <fileheader>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Locust.Spatial;

public static partial class KoreNumeric1DArrayOps<T> where T : struct, INumber<T>
{
    // --------------------------------------------------------------------------------------------
    // MARK: Create Array Series
    // --------------------------------------------------------------------------------------------

    // Functions to create arrays based on start and step values.
    // Usage: var array = KoreNumeric1DArrayOps.CreateArray(0, 1, 10);
    public static KoreNumeric1DArray<T> CreateArrayByStep(T start, T step, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        var array = new KoreNumeric1DArray<T>(count);
        for (int i = 0; i < count; i++)
        {
            array[i] = start + step * T.CreateChecked(i);
        }
        return array;
    }

    // --------------------------------------------------------------------------------------------

    // Create a sequence of values from start to end, with a best-fit division of the remaining range between the two.
    // Upscales to double precision to avoid integer rounding hassles.
    // Usage: var array = KoreNumeric1DArrayOps<int>.CreateArrayByCount(0, 8, 4) => [0, 2, 5, 8]
    public static KoreNumeric1DArray<T> CreateArrayByCount(T start, T end, int count)
    {
        if (count < 2) throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 2.");

        var array = new KoreNumeric1DArray<T>(count);

        // Promote to double for precise interpolation
        double startD = double.CreateChecked(start);
        double endD = double.CreateChecked(end);
        double range = endD - startD;

        for (int i = 0; i < count; i++)
        {
            double t = (double)i / (count - 1);
            double value = startD + range * t;

            array[i] = T.CreateChecked(value); // Cast back to T
        }

        return array;
    }

    // --------------------------------------------------------------------------------------------

    // Given the min and max values, and a total number of entries (that clearly must be 2 of greater),
    // construct a list of values that are evenly spaced between the two.
    // Note the range can be descending, so we don't assume min < max.

    // Usage: KoreNumeric1DArray<double> = KoreNumeric1DArrayOps<double>.ListForRange(0.0, 10.0, 5);
    public static KoreNumeric1DArray<T> ListForRange(T valstart, T valend, int count)
    {
        // Check we have sufficient entries to create a range
        if (count < 2) throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 2.");
        if (count > 1000000) throw new ArgumentOutOfRangeException(nameof(count), "Count must not exceed 1,000,000. Basic memory protection check.");

        // create the returned array
        var array = new KoreNumeric1DArray<T>(count);

        // Convert start/end to double for precision and interpolation
        double start = double.CreateChecked(valstart);
        double end = double.CreateChecked(valend);
        double step = (end - start) / (count - 1);

        // Explicitly set the first value to the start value
        array[0] = T.CreateChecked(valstart);

        // Loop through the array setting the values based on the step size.
        for (int i = 1; i < count; i++)
            array[i] = T.CreateChecked(start + step * i);

        // Explicitly set the last value to the end value, to ensure we don't have rounding errors.
        array[count - 1] = T.CreateChecked(valend);

        // Return the array
        return array;
    }

}


