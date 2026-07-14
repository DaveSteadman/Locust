namespace Locust;

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

public sealed record RegisterQuadTreeValueRequest(
    string Position,
    double Value,
    double DecaySecs)
{
    public QuadTreePosition ToQuadTreePosition()
    {
        return QuadTreePosition.Parse(Position);
    }
}

public sealed record RegisterQuadTreeValueResponse(
    string Position,
    LLPoint Center,
    double RadiusDegs,
    double Value,
    double DecaySecs,
    LLRect NodeBounds);

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

public sealed record TreeRectQueryRequest(
    string CenterPosition,
    int WidthCount,
    int HeightCount)
{
    public QuadTreePosition ToQuadTreePosition()
    {
        return QuadTreePosition.Parse(CenterPosition);
    }
}

public sealed record TreeRectCellResponse(
    string RequestedPosition,
    string ResolvedPosition,
    LLRect Bounds,
    double Strength,
    bool HasExactNode,
    bool IsInsideTree);

public sealed record TreeRectQueryResponse(
    string CenterPosition,
    int WidthCount,
    int HeightCount,
    TreeRectCellResponse[][] Nodes);

public sealed record ErrorResponse(string Error, string? ParamName);
