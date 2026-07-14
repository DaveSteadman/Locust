namespace Locust;

public sealed class QuadTreeApiService
{
    private readonly object _sync = new();
    private readonly List<int> _pingNodes = [];
    private readonly QuadTree<QuadTreePayload> _tree = new();

    // --------------------------------------------------------------------------------------------
    // MARK: Register
    // --------------------------------------------------------------------------------------------

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
            _tree.GetNode(node).Bounds);
    }

    public RegisterQuadTreeValueResponse RegisterQuadTreeValue(RegisterQuadTreeValueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var position = request.ToQuadTreePosition();
        var nodeIndex = QuadTreeNavigation.SetPayloadValue(_tree, position, request.Value, request.DecaySecs);
        var nodeBounds = _tree.GetNode(nodeIndex).Bounds;

        return new RegisterQuadTreeValueResponse(
            position.ToString(),
            position.ToCenter(_tree.Root.Bounds),
            position.ToRadiusDegs(_tree.Root.Bounds),
            request.Value,
            request.DecaySecs,
            nodeBounds);
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

    // --------------------------------------------------------------------------------------------
    // MARK: Query
    // --------------------------------------------------------------------------------------------

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

    public TreeRectQueryResponse QueryRect(TreeRectQueryRequest request)
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

        var centerPosition = request.ToQuadTreePosition();
        var rect = QuadTreeNavigation.GetStrengthRect(_tree, centerPosition, request.WidthCount, request.HeightCount);

        return new TreeRectQueryResponse(
            centerPosition.ToString(),
            request.WidthCount,
            request.HeightCount,
            ToJaggedArray(rect));
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Conversion
    // --------------------------------------------------------------------------------------------

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

    private static TreeRectCellResponse[][] ToJaggedArray(QuadTreeRectCell<double>[,] rect)
    {
        var height = rect.GetLength(1);
        var width = rect.GetLength(0);
        var rows = new TreeRectCellResponse[height][];

        for (var y = 0; y < height; y++)
        {
            rows[y] = new TreeRectCellResponse[width];
            for (var x = 0; x < width; x++)
            {
                var cell = rect[x, y];
                rows[y][x] = new TreeRectCellResponse(
                    cell.RequestedPosition.ToString(),
                    cell.ResolvedPosition.ToString(),
                    cell.Bounds,
                    cell.Value,
                    cell.HasExactNode,
                    cell.IsInsideTree);
            }
        }

        return rows;
    }
}
