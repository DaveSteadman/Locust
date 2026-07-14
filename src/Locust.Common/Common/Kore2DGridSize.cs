// <fileheader>



// Class to define a 2D grid and a position within it:
// - A grid is a number of cells vertically and horizontally.
// - A position is a 0 to n-1 index in each axis

namespace Locust;

public struct Kore2DGridSize
{
    public int Width; // Size of the grid. EG 3 cells wide
    public int Height;

    public readonly float AspectRatio => (float)Width / (float)Height;

    public Kore2DGridSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    // Override ToString to report the object content
    public override string ToString()
    {
        return $"Width:{Width} Height:{Height}";
    }
}
