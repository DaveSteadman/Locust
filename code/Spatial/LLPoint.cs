using System.Globalization;

namespace Locust.Spatial;

public record struct LLPoint
{
    public double LonDegs { get; set; }
    public double LatDegs { get; set; }

    public static LLPoint Zero => new(0d, 0d);

    public LLPoint(double inloDegs, double inlaDegs)
    {
        LatDegs = inlaDegs;
        LonDegs = inloDegs;
    }

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"(Lon:{LonDegs.ToString("G17", CultureInfo.InvariantCulture)}, Lat:{LatDegs.ToString("G17", CultureInfo.InvariantCulture)})");
    }
}
