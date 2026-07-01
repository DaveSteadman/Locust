using System.Globalization;

namespace Locust.Spatial;

public readonly record struct LLRect(double TLLonDegs, double TLLatDegs, double WidthDegs, double HeightDegs)
{
    private const int MaxDisplayDecimalPlaces = 15;

    public static LLRect EarthDegrees => new(-180d, -90d, 360d, 180d);

    // --------------------------------------------------------------------------------------------

    public double LLonDegs => TLLonDegs;
    public double TLatDegs => TLLatDegs;
    public double RLonDegs => TLLonDegs + WidthDegs;
    public double BLatDegs => TLLatDegs + HeightDegs;
    public double MidLonDegs => TLLonDegs + (WidthDegs / 2d);
    public double MidLatDegs => TLLatDegs + (HeightDegs / 2d);
    public double MaxSpanDegrees => Math.Max(WidthDegs, HeightDegs);

    // --------------------------------------------------------------------------------------------

    public static LLRect Zero => new(0d, 0d, 0d, 0d);

    // --------------------------------------------------------------------------------------------

    public bool IsValid()
    {
        bool LLonValid = LLonDegs >= -180d && LLonDegs <= 180d;
        bool RLonValid = RLonDegs >= -180d && RLonDegs <= 180d;
        bool TLatValid = TLatDegs >= -90d && TLatDegs  <= 90d;
        bool BLatValid = BLatDegs >= -90d && BLatDegs  <= 90d;

        bool LonValid = LLonValid && RLonValid && (LLonDegs <= RLonDegs);
        bool LatValid = TLatValid && BLatValid && (TLatDegs <= BLatDegs);

        return LonValid && LatValid;
    }

    // --------------------------------------------------------------------------------------------

    public bool Contains(LLPoint point)
    {
        return point.LonDegs >= LLonDegs &&
               point.LonDegs <= RLonDegs &&
               point.LatDegs >= TLatDegs &&
               point.LatDegs <= BLatDegs;
    }

    public bool Contains(LLRect other)
    {
        return other.LLonDegs >= LLonDegs &&
               other.RLonDegs <= RLonDegs &&
               other.TLatDegs >= TLatDegs &&
               other.BLatDegs <= BLatDegs;
    }

    public bool Intersects(LLRect other)
    {
        return !(other.LLonDegs > RLonDegs ||
                 other.RLonDegs < LLonDegs ||
                 other.TLatDegs > BLatDegs ||
                 other.BLatDegs < TLatDegs);
    }

    // --------------------------------------------------------------------------------------------

    public LLRect TopLeft()     { return new LLRect(TLLonDegs,  TLLatDegs,  WidthDegs / 2d, HeightDegs / 2d); }
    public LLRect TopRight()    { return new LLRect(MidLonDegs, TLLatDegs,  WidthDegs / 2d, HeightDegs / 2d); }
    public LLRect BottomLeft()  { return new LLRect(TLLonDegs,  MidLatDegs, WidthDegs / 2d, HeightDegs / 2d); }
    public LLRect BottomRight() { return new LLRect(MidLonDegs, MidLatDegs, WidthDegs / 2d, HeightDegs / 2d); }

    // --------------------------------------------------------------------------------------------

    public override string ToString()
    {
        var decimalPlaces = Math.Min(
            MaxDisplayDecimalPlaces,
            Math.Max(
                GetRequiredDecimalPlaces(WidthDegs),
                GetRequiredDecimalPlaces(HeightDegs)));

        var format = $"F{decimalPlaces}";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"({TLLonDegs.ToString(format, CultureInfo.InvariantCulture)}, {TLLatDegs.ToString(format, CultureInfo.InvariantCulture)}, {WidthDegs.ToString(format, CultureInfo.InvariantCulture)}, {HeightDegs.ToString(format, CultureInfo.InvariantCulture)})");
    }

    // --------------------------------------------------------------------------------------------

    private static int GetRequiredDecimalPlaces(double value)
    {
        if (value == 0d)
        {
            return 0;
        }

        var text = ((decimal)Math.Abs(value)).ToString(CultureInfo.InvariantCulture);

        var decimalIndex = text.IndexOf('.');
        if (decimalIndex < 0)
        {
            return 0;
        }

        var fractional = text[(decimalIndex + 1)..].TrimEnd('0');
        return fractional.Length;
    }
}
