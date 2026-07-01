// <fileheader>

using System;
using System.Numerics;

namespace Locust.Spatial;

// Class to define a 2D grid and a position within it:
// - A grid is a number of cells vertically and horizontally.
// - A position is a 0 to n-1 index in each axis

public struct KoreNumeric2DPosition<T> where T : INumber<T>
{
    public T PosX;  // X position within the grid. e.g. 0 to 2 in a 3 cell wide grid
    public T PosY;

    public T ExtentX;  // Size of the grid, e.g. 3 cells wide, from 0.
    public T ExtentY;

    public readonly T Area => ExtentX * ExtentY;

    public readonly double FractionX    => ExtentX == T.One ? 0.0 : Convert.ToDouble(PosX) / Convert.ToDouble(ExtentX - T.One);
    public readonly double FractionY    => ExtentY == T.One ? 0.0 : Convert.ToDouble(PosY) / Convert.ToDouble(ExtentY - T.One);
    public readonly double AspectRatio  => Convert.ToDouble(ExtentX) / Convert.ToDouble(ExtentY);

    public KoreNumeric2DPosition(T posX, T posY, T extentX, T extentY)
    {
        PosX = posX;
        PosY = posY;
        ExtentX = extentX;
        ExtentY = extentY;
    }

    public KoreNumeric2DPosition(T posX, T posY, KoreNumeric2DSize<T> size)
    {
        PosX = posX;
        PosY = posY;
        ExtentX = size.Width;
        ExtentY = size.Height;
    }

    public override readonly string ToString()
    {
        return $"PosX:{PosX} PosY:{PosY} ExtentX:{ExtentX} ExtentY:{ExtentY}";
    }
}
