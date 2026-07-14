namespace Locust;

public readonly record struct QuadTreePosition
{
    // --------------------------------------------------------------------------------------------
    // MARK: Constants
    // --------------------------------------------------------------------------------------------

    public const char TopLeftDigit     = '1';
    public const char TopRightDigit    = '2';
    public const char BottomLeftDigit  = '3';
    public const char BottomRightDigit = '4';

    // --------------------------------------------------------------------------------------------
    // MARK: Constructors
    // --------------------------------------------------------------------------------------------

    public QuadTreePosition(string path)
    {
        Path = ValidatePath(path);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: State
    // --------------------------------------------------------------------------------------------

    public static QuadTreePosition Root => new(string.Empty);

    public string Path { get; }

    public int Depth => Path.Length;

    public override string ToString() { return Path; }

    // --------------------------------------------------------------------------------------------
    // MARK: Parsing
    // --------------------------------------------------------------------------------------------

    public static QuadTreePosition Parse(string path)
    {
        return new QuadTreePosition(path);
    }

    public static bool TryParse(string? path, out QuadTreePosition position)
    {
        if (path is null)
        {
            position = default;
            return false;
        }

        try
        {
            position = new QuadTreePosition(path);
            return true;
        }
        catch (ArgumentException)
        {
            position = default;
            return false;
        }
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Navigation
    // --------------------------------------------------------------------------------------------

    public QuadTreePosition Append(char quadrantDigit)
    {
        ValidateQuadrantDigit(quadrantDigit);
        return new QuadTreePosition(Path + quadrantDigit);
    }

    public LLPoint ToCenter()
    {
        return ToCenter(LLRect.EarthDegrees);
    }

    public LLRect ToBounds(LLRect rootBounds)
    {
        var bounds = rootBounds;

        foreach (var quadrantDigit in Path)
        {
            bounds = quadrantDigit switch
            {
                TopLeftDigit     => bounds.TopLeft(),
                TopRightDigit    => bounds.TopRight(),
                BottomLeftDigit  => bounds.BottomLeft(),
                BottomRightDigit => bounds.BottomRight(),
                _ => throw new InvalidOperationException("QuadTreePosition contained an invalid quadrant digit.")
            };
        }

        return bounds;
    }

    public LLPoint ToCenter(LLRect rootBounds)
    {
        var bounds = ToBounds(rootBounds);
        return new LLPoint(bounds.MidLonDegs, bounds.MidLatDegs);
    }

    public double ToRadiusDegs()
    {
        return ToRadiusDegs(LLRect.EarthDegrees);
    }

    public double ToRadiusDegs(LLRect rootBounds)
    {
        return ToBounds(rootBounds).MaxSpanDegrees;
    }

    public static QuadTreePosition FromPointRadius(LLPoint center, double radiusDegs)
    {
        return FromPointRadius(LLRect.EarthDegrees, center, radiusDegs);
    }

    public static QuadTreePosition FromPointRadius(LLRect rootBounds, LLPoint center, double radiusDegs)
    {
        if (radiusDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusDegs), "Radius must be greater than zero.");
        }

        if (!rootBounds.Contains(center))
        {
            throw new ArgumentOutOfRangeException(nameof(center), "Point must be inside the tree bounds.");
        }

        var position = Root;
        var bounds = rootBounds;

        while (bounds.MaxSpanDegrees > radiusDegs)
        {
            var topLeftBounds = bounds.TopLeft();
            if (topLeftBounds.Contains(center))
            {
                position = position.Append(TopLeftDigit);
                bounds = topLeftBounds;
                continue;
            }

            var topRightBounds = bounds.TopRight();
            if (topRightBounds.Contains(center))
            {
                position = position.Append(TopRightDigit);
                bounds = topRightBounds;
                continue;
            }

            var bottomLeftBounds = bounds.BottomLeft();
            if (bottomLeftBounds.Contains(center))
            {
                position = position.Append(BottomLeftDigit);
                bounds = bottomLeftBounds;
                continue;
            }

            var bottomRightBounds = bounds.BottomRight();
            if (bottomRightBounds.Contains(center))
            {
                position = position.Append(BottomRightDigit);
                bounds = bottomRightBounds;
                continue;
            }

            throw new InvalidOperationException("Point was not contained by any child node.");
        }

        return position;
    }

    public void GetGridCoordinates(out int column, out int row)
    {
        column = 0;
        row = 0;

        foreach (var quadrantDigit in Path)
        {
            column <<= 1;
            row <<= 1;

            switch (quadrantDigit)
            {
                case TopLeftDigit:
                    break;

                case TopRightDigit:
                    column |= 1;
                    break;

                case BottomLeftDigit:
                    row |= 1;
                    break;

                case BottomRightDigit:
                    column |= 1;
                    row |= 1;
                    break;

                default:
                    throw new InvalidOperationException("QuadTreePosition contained an invalid quadrant digit.");
            }
        }
    }

    public static QuadTreePosition FromGridCoordinates(int depth, int column, int row)
    {
        if (depth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be zero or greater.");
        }

        var dimension = 1 << depth;
        if ((uint)column >= (uint)dimension)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column must be inside the grid for the supplied depth.");
        }

        if ((uint)row >= (uint)dimension)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "Row must be inside the grid for the supplied depth.");
        }

        if (depth == 0)
        {
            return Root;
        }

        var digits = new char[depth];
        for (var bit = depth - 1; bit >= 0; bit--)
        {
            var columnBit = (column >> bit) & 1;
            var rowBit = (row >> bit) & 1;

            digits[depth - bit - 1] = (columnBit, rowBit) switch
            {
                (0, 0) => TopLeftDigit,
                (1, 0) => TopRightDigit,
                (0, 1) => BottomLeftDigit,
                (1, 1) => BottomRightDigit,
                _ => throw new InvalidOperationException("Unexpected grid coordinate bits.")
            };
        }

        return new QuadTreePosition(new string(digits));
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Validation
    // --------------------------------------------------------------------------------------------

    private static string ValidatePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        foreach (var quadrantDigit in path)
        {
            ValidateQuadrantDigit(quadrantDigit);
        }

        return path;
    }

    private static void ValidateQuadrantDigit(char quadrantDigit)
    {
        if (quadrantDigit is not (TopLeftDigit or TopRightDigit or BottomLeftDigit or BottomRightDigit))
        {
            throw new ArgumentOutOfRangeException(nameof(quadrantDigit), "QuadTreePosition digits must be 1, 2, 3, or 4.");
        }
    }
}
