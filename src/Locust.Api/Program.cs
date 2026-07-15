namespace Locust;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<QuadTreeApiService>();
        builder.Services.AddHostedService<QuadTreeDecayWorker>();

        var app = builder.Build();

        app.MapGet("/", () => Results.Ok(new
        {
            service = "Locust",
            endpoints = new[]
            {
                "POST /pings",
                "POST /pings/batch",
                "POST /values/quadtree",
                "POST /queries/grid",
                "POST /queries/rect"
            }
        }));

        app.MapPost("/pings", (RegisterPingRequest request, QuadTreeApiService service) =>
        {
            try
            {
                return Results.Ok(service.RegisterPing(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message, ex.ParamName));
            }
        });

        app.MapPost("/pings/batch", (RegisterPingsRequest request, QuadTreeApiService service) =>
        {
            try
            {
                return Results.Ok(service.RegisterPings(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message, ex.ParamName));
            }
        });

        app.MapPost("/values/quadtree", (RegisterQuadTreeValueRequest request, QuadTreeApiService service) =>
        {
            try
            {
                return Results.Ok(service.RegisterQuadTreeValue(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message, ex.ParamName));
            }
        });

        app.MapPost("/queries/grid", (GridQueryRequest request, QuadTreeApiService service) =>
        {
            try
            {
                return Results.Ok(service.QueryGrid(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message, ex.ParamName));
            }
        });

        app.MapPost("/queries/rect", (TreeRectQueryRequest request, QuadTreeApiService service) =>
        {
            try
            {
                return Results.Ok(service.QueryRect(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message, ex.ParamName));
            }
        });

        app.Run();
    }
}

internal sealed class QuadTreeDecayWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private readonly QuadTreeApiService _service;
    private readonly ILogger<QuadTreeDecayWorker> _logger;

    public QuadTreeDecayWorker(QuadTreeApiService service, ILogger<QuadTreeDecayWorker> logger)
    {
        _service = service;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var retained = _service.DecayAndPruneExpiredPings();
                _logger.LogDebug("QuadTree decay pass retained {RetainedPingNodes} active ping nodes.", retained);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QuadTree decay pass failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
