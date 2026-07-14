namespace Locust;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<QuadTreeApiService>();

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
