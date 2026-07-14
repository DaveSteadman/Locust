// <fileheader>

using System;
using System.Numerics;

namespace Locust;

// Class to define a 2D grid and a position within it:
// - A grid is a number of cells vertically and horizontally.
// - A position is a 0 to n-1 index in each axis

public struct KoreNumeric2DSize<T> where T : INumber<T>
{
    public T Width;  // Size of the grid, e.g. 3 cells wide
    public T Height;

    public readonly T AspectRatio => Width / Height;
    public readonly T Area => Width * Height;

    public KoreNumeric2DSize(T width, T height)
    {
        Width = width;
        Height = height;
    }

    public override readonly string ToString()
    {
        return $"Width:{Width} Height:{Height}";
    }
}
