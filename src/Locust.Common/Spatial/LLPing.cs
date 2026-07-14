

namespace Locust;

public record struct LLPing
{
    public LLPoint Center     { get; set; }
    public double  Strength   { get; set; }
    public double  RadiusDegs { get; set; }
    public double  DecaySecs  { get; set; }


    public LLPing(LLPoint center, double strength, double radiusDegs, double decaySecs)
    {
        Center     = center;
        Strength   = strength;
        RadiusDegs = radiusDegs;
        DecaySecs  = decaySecs;
    }

    public static LLPing Zero => new (LLPoint.Zero, 0d, 0d, 0d);

}
