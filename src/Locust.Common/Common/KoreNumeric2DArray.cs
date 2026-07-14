// <fileheader>

// global using KoreFloat2DArray  = KoreNumeric2DArray<float>;
// global using KoreDouble2DArray = KoreNumeric2DArray<double>;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml;

namespace Locust;

// ------------------------------------------------------------------------------------------------
// MARK: Derived Types
// ------------------------------------------------------------------------------------------------

public class KoreInt2DArray : KoreNumeric2DArray<int>
{
    public KoreInt2DArray() : base(0, 0) { } // Default constructor for empty array
    public KoreInt2DArray(int width, int height) : base(width, height) { }
    public KoreInt2DArray(int[,] initialData) : base(initialData) { }
    public KoreInt2DArray(KoreNumeric2DArray<int> other) : base(other) { }
}

public class KoreFloat2DArray : KoreNumeric2DArray<float>
{
    public KoreFloat2DArray() : base(0, 0) { } // Default constructor for empty array
    public KoreFloat2DArray(int width, int height) : base(width, height) { }
    public KoreFloat2DArray(float[,] initialData) : base(initialData) { }
    public KoreFloat2DArray(KoreNumeric2DArray<float> other) : base(other) { }
}

public class KoreDouble2DArray : KoreNumeric2DArray<double>
{
    public KoreDouble2DArray() : base(0, 0) { } // Default constructor for empty array
    public KoreDouble2DArray(int width, int height) : base(width, height) { }
    public KoreDouble2DArray(double[,] initialData) : base(initialData) { }
    public KoreDouble2DArray(KoreNumeric2DArray<double> other) : base(other) { }
}

// ------------------------------------------------------------------------------------------------

public partial class KoreNumeric2DArray<T> where T : struct, INumber<T>
{
    // Note that is all design and accesses 0,0 is considered a top-left corner.
    private T[,] Data;

    public int Width { get; }
    public int Height { get; }

    public int Count => Width * Height;
    public Kore2DGridSize Size => new Kore2DGridSize(Width, Height);

    public enum Edge { Undefined, Top, Bottom, Left, Right };

    public bool Populated { get; set; }

    // --------------------------------------------------------------------------------------------
    // MARK: Constructors
    // --------------------------------------------------------------------------------------------

    public KoreNumeric2DArray()
    {
        Width     = 1;
        Height    = 1;
        Data      = new T[1, 1];
        Populated = false;
    }

    public KoreNumeric2DArray(int width, int height)
    {
        if (width < 1 || height < 1)
            throw new ArgumentException("Width and Height must be greater than 0.");

        Width     = width;
        Height    = height;
        Data      = new T[Height, Width];
        Populated = false;
    }

    public KoreNumeric2DArray(T[,] initialData)
    {
        Height = initialData.GetLength(0);
        Width  = initialData.GetLength(1);
        Data   = new T[Height, Width];

        Array.Copy(initialData, Data, initialData.Length);
        Populated = true;
    }

    public KoreNumeric2DArray(KoreNumeric2DArray<T> other)
    {
        Height = other.Height;
        Width  = other.Width;
        Data   = new T[Height, Width];

        Array.Copy(other.Data, Data, other.Data.Length);
        Populated = other.Populated;
    }

    // --------------------------------------------------------------------------------------------
    // Indexer
    // --------------------------------------------------------------------------------------------

    public T this[int y, int x]
    {
        get => Data[y, x];
        set => Data[y, x] = value;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Ops
    // --------------------------------------------------------------------------------------------

    public T MinVal() => Data.Cast<T>().Min();
    public T MaxVal() => Data.Cast<T>().Max();
    public T Sum() => Data.Cast<T>().Aggregate(T.Zero, (current, value) => current + value);

    public KoreNumeric2DArray<T> ScaleValues(T newMin, T newMax)
    {
        T oldMin = MinVal();
        T oldMax = MaxVal();
        T oldRange = oldMax - oldMin;
        T newRange = newMax - newMin;

        KoreNumeric2DArray<T> scaledArray = new KoreNumeric2DArray<T>(Width, Height);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                T value = (Data[y, x] - oldMin) / oldRange * newRange + newMin;
                scaledArray[y, x] = value;
            }
        }

        return scaledArray;
    }

    public KoreNumeric2DArray<T> CropValuesToRange(T newMin, T newMax)
    {
        KoreNumeric2DArray<T> croppedArray = new KoreNumeric2DArray<T>(Width, Height);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                T value = Data[y, x];
                if (value < newMin) value = newMin;
                if (value > newMax) value = newMax;
                croppedArray[y, x] = value;
            }
        }

        return croppedArray;
    }

    public KoreNumeric2DArray<T> CropValuesToRange(KoreNumericRange<T> range) => CropValuesToRange(range.Min, range.Max);

    // --------------------------------------------------------------------------------------------
    // MARK: Set Value
    // --------------------------------------------------------------------------------------------

    public void SetAll(T value)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                Data[y, x] = value;

        Populated = true;
    }

    public void SetRow(int row, T value)
    {
        for (int x = 0; x < Width; x++)
            Data[row, x] = value;
    }

    public void SetCol(int col, T value)
    {
        for (int y = 0; y < Height; y++)
            Data[y, col] = value;
    }

    public void SetAllNoise(T minVal, T maxVal)
    {
        Random random = new Random();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                T val = KoreNumericUtils.RandomInRange<T>(minVal, maxVal);
                Data[y, x] = val;
            }
        }

        Populated = true;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Flip
    // --------------------------------------------------------------------------------------------

    public KoreNumeric2DArray<T> Transpose()
    {
        KoreNumeric2DArray<T> transposed = new KoreNumeric2DArray<T>(Height, Width);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                transposed[y, x] = Data[x, y];
            }
        }

        return transposed;
    }

    public KoreNumeric2DArray<T> ReverseRows()
    {
        KoreNumeric2DArray<T> reversed = new KoreNumeric2DArray<T>(Width, Height);

        for (int y = 0; y < Height; y++)
        {
            int destinationY = Height - 1 - y;
            for (int x = 0; x < Width; x++)
            {
                reversed[destinationY, x] = Data[y, x];
            }
        }

        return reversed;
    }

    public KoreNumeric2DArray<T> ReverseCols()
    {
        KoreNumeric2DArray<T> reversed = new KoreNumeric2DArray<T>(Width, Height);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int destinationX = Width - 1 - x;
                reversed[y, destinationX] = Data[y, x];
            }
        }

        return reversed;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Interpolation
    // --------------------------------------------------------------------------------------------

    // Get value from the grid, based on x,y fractions interpolated around the surrounding
    // values. Corners are named for clarity: topLeft, topRight, bottomLeft, bottomRight.

    public T InterpolatedValue(T fractionx, T fractiony)
    {
        // Calculate indices
        T scaleX = T.CreateChecked(Width - 1);
        T scaleY = T.CreateChecked(Height - 1);
        int x = int.CreateChecked(fractionx * scaleX);
        int y = int.CreateChecked(fractiony * scaleY);

        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        // Calculate fractions for interpolation
        T fx = fractionx * scaleX - T.CreateChecked(x);
        T fy = fractiony * scaleY - T.CreateChecked(y);

        // Get surrounding values (corners)
        int minX = x;
        int minY = y;
        int maxX = Math.Clamp(x + 1, 0, Width - 1);
        int maxY = Math.Clamp(y + 1, 0, Height - 1);
        T topLeft     = Data[minY, minX];
        T topRight    = Data[minY, maxX];
        T bottomLeft  = Data[maxY, minX];
        T bottomRight = Data[maxY, maxX];

        // Perform bilinear interpolation
        T interpolatedValue = (T.One - fx) * (T.One - fy) * topLeft
                            + fx * (T.One - fy) * topRight
                            + (T.One - fx) * fy * bottomLeft
                            + fx * fy * bottomRight;

        return interpolatedValue;
    }

    // Return a list of the values that would be used in the interpolation of a point.
    // - This allows us to see, and importantly validate, the values that would be used for interpolation.

    public List<T> InterpolationValues(T fractionx, T fractiony)
    {
        List<T> interpolatedValues = new List<T>();

        // Calculate indices
        T scaleX = T.CreateChecked(Width - 1);
        T scaleY = T.CreateChecked(Height - 1);
        int x = int.CreateChecked(fractionx * scaleX);
        int y = int.CreateChecked(fractiony * scaleY);

        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        // Calculate fractions for interpolation
        T fx = fractionx * scaleX - T.CreateChecked(x);
        T fy = fractiony * scaleY - T.CreateChecked(y);

        // Get surrounding values (corners)
        int minX = x;
        int minY = y;
        int maxX = Math.Clamp(x + 1, 0, Width - 1);
        int maxY = Math.Clamp(y + 1, 0, Height - 1);
        T topLeft     = Data[minY, minX];
        T topRight    = Data[minY, maxX];
        T bottomLeft  = Data[maxY, minX];
        T bottomRight = Data[maxY, maxX];

        interpolatedValues.Add(topLeft);
        interpolatedValues.Add(topRight);
        interpolatedValues.Add(bottomLeft);
        interpolatedValues.Add(bottomRight);

        return interpolatedValues;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: String
    // --------------------------------------------------------------------------------------------

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                sb.Append(Data[y, x] + " ");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
