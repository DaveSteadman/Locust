// <fileheader>


// global using KoreFloat2DArray  = KoreNumeric2DArray<float>;
// global using KoreDouble2DArray = KoreNumeric2DArray<double>;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Locust;

public partial class KoreNumeric2DArray<T> where T : struct, INumber<T>
{
    public KoreNumeric2DArray<T> GetInterpolatedGrid(int inNewSizeX, int inNewSizeY)
    {
        KoreNumeric2DArray<T> retGrid = new KoreNumeric2DArray<T>(inNewSizeX, inNewSizeY);

        if (Width <= 3 || Height <= 3)
            throw new Exception("Grid is too small to interpolate.");

        T xFactor = T.CreateChecked(Width) / T.CreateChecked(inNewSizeX);
        T yFactor = T.CreateChecked(Height) / T.CreateChecked(inNewSizeY);
        float xFactorFloat = float.CreateChecked(xFactor);
        float yFactorFloat = float.CreateChecked(yFactor);

        for (int j = 0; j < inNewSizeY; j++)
        {
            int yIndex = Math.Min((int)(j * yFactorFloat), Height - 2);
            T yRemainder = T.CreateChecked(j) * yFactor - T.CreateChecked(yIndex);

            for (int i = 0; i < inNewSizeX; i++)
            {
                int xIndex = Math.Min((int)(i * xFactorFloat), Width - 2);
                T xRemainder = T.CreateChecked(i) * xFactor - T.CreateChecked(xIndex);

                T a = Data[yIndex,     xIndex];
                T b = Data[yIndex,     xIndex + 1];
                T c = Data[yIndex + 1, xIndex];
                T d = Data[yIndex + 1, xIndex + 1];

                T newVal = (T.One - xRemainder) * (T.One - yRemainder) * a +
                           xRemainder * (T.One - yRemainder) * b +
                           (T.One - xRemainder) * yRemainder * c +
                           xRemainder * yRemainder * d;

                retGrid[j, i] = newVal;
            }
        }
        return retGrid;
    }

    // --------------------------------------------------------------------------------------------

    public KoreNumeric2DArray<T> GetSubgrid(int startX, int startY, int subgridWidth, int subgridHeight)
    {
        // Clamp the start values to fit in the grid size
        startX = Math.Clamp(startX, 0, Width - 1);
        startY = Math.Clamp(startY, 0, Height - 1);

        // Ensure that the subgrid does not extend beyond the bounds of the main grid
        int endX = Math.Min(startX + subgridWidth, Width);
        int endY = Math.Min(startY + subgridHeight, Height);

        // Calculate actual width and height of the subgrid
        subgridWidth  = endX - startX;
        subgridHeight = endY - startY;

        // Create the return object
        KoreNumeric2DArray<T> outGrid = new KoreNumeric2DArray<T>(subgridWidth, subgridHeight);

        for (int x = 0; x < subgridWidth; x++)
        {
            int srcX = x + startX;

            for (int y = 0; y < subgridHeight; y++)
            {
                int srcY = y + startY;
                outGrid[y, x] = Data[srcY, srcX];
            }
        }
        return outGrid;
    }

    // --------------------------------------------------------------------------------------------

    public KoreNumeric2DArray<T> GetInterpolatedSubgrid(Kore2DGridPos gridPos, int subgridWidth, int subgridHeight)
    {
        int totalSubgridWidth  = gridPos.Width * subgridWidth;
        int totalSubgridHeight = gridPos.Height * subgridHeight;

        KoreNumeric2DArray<T> interpolatedGrid = GetInterpolatedGrid(totalSubgridWidth, totalSubgridHeight);

        int subgridStartX = gridPos.PosX * subgridWidth;
        int subgridStartY = gridPos.PosY * subgridHeight;

        return interpolatedGrid.GetSubgrid(subgridStartX, subgridStartY, subgridWidth, subgridHeight);
    }

    // --------------------------------------------------------------------------------------------

    public KoreNumeric2DArray<T>[,] GetInterpolatedSubGridCellWithOverlap(int inNumSubgridCols, int inNumSubgridRows, int inSubgridSizeX, int inSubgridSizeY)
    {
        int totalSubgridWidth  = inNumSubgridCols * (inSubgridSizeX - 1) + 1 + 1;
        int totalSubgridHeight = inNumSubgridRows * (inSubgridSizeY - 1) + 1 + 1;

        KoreNumeric2DArray<T> interpolatedGrid = GetInterpolatedGrid(totalSubgridWidth, totalSubgridHeight);

        KoreNumeric2DArray<T>[,] subGrid = new KoreNumeric2DArray<T>[inNumSubgridCols, inNumSubgridRows];

        for (int j = 0; j < inNumSubgridRows; j++)
        {
            for (int i = 0; i < inNumSubgridCols; i++)
            {
                int subgridStartY = j * (inSubgridSizeY - 1);
                int subgridStartX = i * (inSubgridSizeX - 1);

                subGrid[j, i] = interpolatedGrid.GetSubgrid(subgridStartX, subgridStartY, inSubgridSizeX, inSubgridSizeY);
            }
        }

        return subGrid;
    }
}
