// <fileheader>



// Class to define a 2D grid and a position within it:
// - A grid is a number of cells vertically and horizontally.
// - A position is a 0 to n-1 index in each axis

namespace Locust;

public struct Kore2DGridPos
{
    public int Width; // Size of the grid. EG 3 cells wide
    public int Height;
    public int PosX;  // Position in the grid. EG 0 to 2
    public int PosY;

    // Get the fractions across the box (starting in top left) for the edges of the Position
    public float TopEdgeFraction => (float)(PosY) / (float)Height;
    public float BottomEdgeFraction => (float)(PosY + 1) / (float)Height;
    public float LeftEdgeFraction => (float)(PosX) / (float)Width;
    public float RightEdgeFraction => (float)(PosX + 1) / (float)Width;

    public Kore2DGridPos(int width, int height, int posX, int posY)
    {
        Width = width;
        Height = height;
        PosX = posX;
        PosY = posY;
    }

    // Override ToString to report the object content
    public override string ToString()
    {
        return $"Width:{Width} Height:{Height} PosX:{PosX} PosY:{PosY}";
    }
}


