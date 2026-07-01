using Locust.Spatial;

namespace Locust.Api;

public sealed record RegisterPingRequest(
    double LonDegs,
    double LatDegs,
    double Strength,
    double RadiusDegs,
    double DecaySecs)
{
    public LLPing ToPing()
    {
        return new LLPing(new LLPoint(LonDegs, LatDegs), Strength, RadiusDegs, DecaySecs);
    }
}

public sealed record RegisterPingsRequest(IReadOnlyList<RegisterPingRequest> Pings);

public sealed record RegisterPingResponse(
    LLPoint Center,
    double Strength,
    double RadiusDegs,
    double DecaySecs,
    LLRect NodeBounds);

public sealed record RegisterPingsResponse(
    int RegisteredCount,
    IReadOnlyList<RegisterPingResponse> Pings);

public sealed record GridQueryRequest(
    double TopLeftLonDegs,
    double TopLeftLatDegs,
    double WidthDegs,
    double HeightDegs,
    int WidthCount,
    int HeightCount)
{
    public LLRect ToBounds()
    {
        return new LLRect(TopLeftLonDegs, TopLeftLatDegs, WidthDegs, HeightDegs);
    }
}

public sealed record GridQueryResponse(
    LLRect Bounds,
    int WidthCount,
    int HeightCount,
    double[][] Values);

public sealed record ErrorResponse(string Error, string? ParamName);
