using Locust.Spatial;

namespace Locust.Api;

public sealed class QuadTreeApiService
{
    private readonly object _sync = new();
    private readonly List<QuadTreeNode> _pingNodes = [];
    private readonly QuadTree _tree = new();

    public RegisterPingResponse RegisterPing(RegisterPingRequest request)
    {
        var ping = request.ToPing();
        var node = PingOperations.Apply(_tree, ping);

        lock (_sync)
        {
            _pingNodes.Add(node);
        }

        return new RegisterPingResponse(
            ping.Center,
            ping.Strength,
            ping.RadiusDegs,
            ping.DecaySecs,
            node.Bounds);
    }

    public RegisterPingsResponse RegisterPings(RegisterPingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Pings);

        var responses = new List<RegisterPingResponse>(request.Pings.Count);
        foreach (var pingRequest in request.Pings)
        {
            responses.Add(RegisterPing(pingRequest));
        }

        return new RegisterPingsResponse(responses.Count, responses);
    }

    public GridQueryResponse QueryGrid(GridQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.WidthCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.WidthCount), "WidthCount must be at least 1.");
        }

        if (request.HeightCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.HeightCount), "HeightCount must be at least 1.");
        }

        var bounds = request.ToBounds();
        var grid = QuadTreeNavigation.StrengthValuesToArray(_tree, bounds, request.WidthCount, request.HeightCount);

        return new GridQueryResponse(
            bounds,
            request.WidthCount,
            request.HeightCount,
            ToJaggedArray(grid));
    }

    private static double[][] ToJaggedArray(KoreDouble2DArray grid)
    {
        var rows = new double[grid.Height][];
        for (var y = 0; y < grid.Height; y++)
        {
            rows[y] = new double[grid.Width];
            for (var x = 0; x < grid.Width; x++)
            {
                rows[y][x] = grid[y, x];
            }
        }

        return rows;
    }
}
