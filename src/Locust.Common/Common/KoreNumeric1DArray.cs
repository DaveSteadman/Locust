// <fileheader>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace Locust;

// ------------------------------------------------------------------------------------------------
// MARK: Derived Types
// ------------------------------------------------------------------------------------------------

// public class KoreInt1DArray : KoreNumeric1DArray<int>
// {
//     public KoreInt1DArray(int newSize) : base(newSize) { }
//     public KoreInt1DArray(int[] initialData) : base(initialData) { }
//     public KoreInt1DArray(KoreNumericRange<int> valueRange, int listSize, ListDirection direction = ListDirection.Forward)
//         : base(valueRange, listSize, direction) { }
// }

// public class KoreFloat1DArray : KoreNumeric1DArray<float>
// {
//     public KoreFloat1DArray(int newSize) : base(newSize) { }
//     public KoreFloat1DArray(float[] initialData) : base(initialData) { }
//     public KoreFloat1DArray(KoreNumericRange<float> valueRange, int listSize, ListDirection direction = ListDirection.Forward)
//         : base(valueRange, listSize, direction) { }
// }

// public class KoreDouble1DArray : KoreNumeric1DArray<double>
// {
//     public KoreDouble1DArray(int newSize) : base(newSize) { }
//     public KoreDouble1DArray(double[] initialData) : base(initialData) { }
//     public KoreDouble1DArray(KoreNumericRange<double> valueRange, int listSize, ListDirection direction = ListDirection.Forward)
//         : base(valueRange, listSize, direction) { }
// }

// ------------------------------------------------------------------------------------------------

public class KoreNumeric1DArray<T> : IEnumerable<T> where T : struct, INumber<T>
{

    private T[] _data;

    public T[] Data
    {
        get => _data;
        private set => _data = value;
    }

    public int Length => Data.Length;

    public enum ListDirection { Forward, Reverse };

    public const int MaxArrayLength = 1_000_000;

    // --------------------------------------------------------------------------------------------
    // MARK: Constructors
    // --------------------------------------------------------------------------------------------

    public KoreNumeric1DArray(int newSize)
    {
        if (newSize < 1 || newSize > MaxArrayLength)
            throw new ArgumentException($"Unexpected Create Size: {newSize}");

        _data = new T[newSize];
    }

    public KoreNumeric1DArray(T[] initialData)
    {
        _data = initialData ?? throw new ArgumentNullException(nameof(initialData));
    }

    public KoreNumeric1DArray(KoreNumericRange<T> valueRange, int listSize, ListDirection direction = ListDirection.Forward)
    {
        _data = new T[listSize];
        T inc = valueRange.IncrementForSize(listSize);

        if (direction == ListDirection.Forward)
        {
            for (int i = 0; i < listSize; i++)
                _data[i] = valueRange.Min + inc * T.CreateChecked(i);
        }
        else
        {
            for (int i = 0; i < listSize; i++)
            {
                int destinationIndex = listSize - (i + 1);
                _data[destinationIndex] = valueRange.Min + inc * T.CreateChecked(i);
            }
        }
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Set Get
    // --------------------------------------------------------------------------------------------

    public void SetValue(int index, T value)
    {
        if (index < 0 || index >= Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

        _data[index] = value;
    }

    public T GetValue(int index)
    {
        if (index < 0 || index >= Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

        return _data[index];
    }

    public T this[int index]
    {
        get => _data[index];
        set => _data[index] = value;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: List Size Management
    // --------------------------------------------------------------------------------------------

    public void Append(T value)
    {
        // If there is currently nothing, initialize the array and add the new value
        if (Length == 0)
        {
            _data = new T[1];
            _data[0] = value;
            return;
        }

        Array.Resize(ref _data, Length + 1);
        _data[Length - 1] = value;
    }

    public void Add(T value) => Append(value);

    public void RemoveAtIndex(int index)
    {
        if (index < 0 || index >= Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

        T[] newData = new T[Length - 1];
        for (int i = 0, j = 0; i < Length; i++)
        {
            if (i != index)
                newData[j++] = _data[i];
        }
        _data = newData;
    }

    // will truncate existing data if newsize shorter, or leave new data uninitialised if newsize longer
    public void SetSize(int newSize)
    {
        if (newSize < 1 || newSize > MaxArrayLength)
            throw new ArgumentException($"Unexpected Resize Size: {newSize}");

        if (newSize == Length) return;

        T[] newData = new T[newSize];
        int copyLength = Math.Min(Length, newSize);
        Array.Copy(_data, newData, copyLength);
        _data = newData;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Ops
    // --------------------------------------------------------------------------------------------

    public T Average() => Sum() / T.CreateChecked(Length);
    public T Min() => _data.Min();
    public T Max() => _data.Max();
    public T Sum() => _data.Aggregate(T.Zero, (current, value) => current + value);
    public T SumAbs() => _data.Aggregate(T.Zero, (current, value) => current + T.Abs(value));

    public KoreNumeric1DArray<T> Multiply(KoreNumeric1DArray<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (Length != other.Length) throw new ArgumentException("Arrays must be the same length", nameof(other));

        T[] result = new T[Length];
        for (int i = 0; i < Length; i++)
            result[i] = _data[i] * other[i];

        return new KoreNumeric1DArray<T>(result);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Get Fraction Ops
    // --------------------------------------------------------------------------------------------

    public T FractionForIndex(int index)
    {
        return T.CreateChecked(index) / T.CreateChecked(Length - 1);
    }

    public int IndexForFraction(T fraction)
    {
        fraction = T.Clamp(fraction, T.Zero, T.One);
        return (int)Math.Round(Convert.ToDouble(fraction * T.CreateChecked(Length - 1)));
    }

    public T InterpolateAtFraction(T fraction)
    {
        double fractionDouble = double.CreateChecked(fraction);
        int lowerIndex = (int)(fractionDouble * (Length - 1));
        int upperIndex = Math.Min(lowerIndex + 1, Length - 1);
        T blend = T.CreateChecked(fractionDouble * (Length - 1) - lowerIndex);

        return _data[lowerIndex] * (T.One - blend) + _data[upperIndex] * blend;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Array Manipulation Ops
    // --------------------------------------------------------------------------------------------

    public KoreNumeric1DArray<T> InterpolatedResize(int newSize)
    {
        if (newSize < 1 || newSize > MaxArrayLength)
            throw new ArgumentException($"Unexpected Resize Size: {newSize}");

        T[] newData = new T[newSize];

        if (Length == 1)
        {
            T value = _data[0];
            for (int i = 0; i < newSize; i++)
                newData[i] = value;
        }
        else
        {
            for (int i = 0; i < newSize; i++)
            {
                T fraction = T.CreateChecked(i) / T.CreateChecked(newSize - 1);
                newData[i] = InterpolateAtFraction(fraction);
            }
        }

        return new KoreNumeric1DArray<T>(newData);
    }

    public KoreNumeric1DArray<T> ArrayForIndexRange(int firstIndex, int lastIndex)
    {
        firstIndex = Math.Clamp(firstIndex, 0, Length - 1);
        lastIndex = Math.Clamp(lastIndex, 0, Length - 1);
        if (firstIndex > lastIndex)
            (firstIndex, lastIndex) = (lastIndex, firstIndex);

        int newSize = lastIndex - firstIndex + 1;
        KoreNumeric1DArray<T> newArray = new KoreNumeric1DArray<T>(newSize);

        for (int i = 0; i < newSize; i++)
            newArray[i] = _data[firstIndex + i];

        return newArray;
    }

    public KoreNumeric1DArray<T> Reverse()
    {
        T[] reversedData = new T[Length];
        for (int i = 0; i < Length; i++)
            reversedData[i] = _data[Length - 1 - i];

        return new KoreNumeric1DArray<T>(reversedData);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: String
    // --------------------------------------------------------------------------------------------

    public string ToString(string format)
    {
        return string.Join(", ", _data.Select(x => ((IFormattable)x).ToString(format, null)));
    }

    // --------------------------------------------------------------------------------------------
    // MARK: IEnumerable Implementation
    // --------------------------------------------------------------------------------------------

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in _data)
            yield return item;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
