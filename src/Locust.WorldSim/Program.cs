namespace Locust;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var options = WorldSimOptions.Parse(args);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<WorldSimulator>();
        builder.Services.AddHostedService<WorldSimulationWorker>();

        var app = builder.Build();

        app.MapGet("/", (WorldSimOptions config) => Results.Ok(new
        {
            service = "Locust.WorldSim",
            bounds = config.Bounds,
            trackCount = config.TrackCount,
            tickIntervalMs = config.TickInterval.TotalMilliseconds,
            endpoints = new[]
            {
                "GET /world",
                "GET /world/snapshot"
            }
        }));

        app.MapGet("/world", (WorldSimulator simulator) => Results.Ok(new
        {
            simulator.Bounds,
            simulator.TrackCount,
            simulator.TickIntervalMs
        }));

        app.MapGet("/world/snapshot", (WorldSimulator simulator) =>
        {
            return Results.Ok(simulator.GetSnapshot());
        });

        app.Run();
    }
}

internal sealed class WorldSimulationWorker : BackgroundService
{
    private readonly WorldSimulator _simulator;
    private readonly WorldSimOptions _options;
    private readonly ILogger<WorldSimulationWorker> _logger;

    public WorldSimulationWorker(WorldSimulator simulator, WorldSimOptions options, ILogger<WorldSimulationWorker> logger)
    {
        _simulator = simulator;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Locust.WorldSim starting with bounds {Bounds}, tracks {TrackCount}, interval {IntervalMs}ms",
            _options.Bounds,
            _options.TrackCount,
            _options.TickInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            _simulator.Advance(_options.TickInterval.TotalSeconds);

            try
            {
                await Task.Delay(_options.TickInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

internal sealed class WorldSimulator
{
    private readonly object _sync = new();
    private readonly WorldSimOptions _options;
    private readonly Random _random;
    private readonly List<TrackState> _tracks;
    private int _tick;
    private DateTimeOffset _generatedAtUtc;

    public WorldSimulator(WorldSimOptions options)
        : this(options, Random.Shared)
    {
    }

    public WorldSimulator(WorldSimOptions options, Random random)
    {
        _options = options;
        _random = random;
        _tracks = CreateTracks(options.TrackCount).ToList();
        _generatedAtUtc = DateTimeOffset.UtcNow;
    }

    public LLRect Bounds => _options.Bounds;
    public int TrackCount => _tracks.Count;
    public double TickIntervalMs => _options.TickInterval.TotalMilliseconds;

    public void Advance(double deltaSeconds)
    {
        lock (_sync)
        {
            foreach (var track in _tracks)
            {
                track.Advance(_options.Bounds, deltaSeconds);
            }

            _tick++;
            _generatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public WorldSnapshotResponse GetSnapshot()
    {
        lock (_sync)
        {
            var tracks = _tracks
                .Select(track => new WorldTrackResponse(
                    track.Id,
                    track.Position,
                    track.CourseDegs,
                    track.SpeedDegsPerSecond,
                    track.VelocityLonDegsPerSecond,
                    track.VelocityLatDegsPerSecond))
                .ToArray();

            return new WorldSnapshotResponse(_options.Bounds, _tick, _generatedAtUtc, tracks);
        }
    }

    private IEnumerable<TrackState> CreateTracks(int count)
    {
        for (var index = 0; index < count; index++)
        {
            var position = new LLPoint(
                NextDouble(_options.Bounds.LLonDegs, _options.Bounds.RLonDegs),
                NextDouble(_options.Bounds.TLatDegs, _options.Bounds.BLatDegs));

            var courseRadians = NextDouble(0d, Math.PI * 2d);
            var speedDegsPerSecond = NextDouble(_options.MinSpeedDegsPerSecond, _options.MaxSpeedDegsPerSecond);
            var velocityLonDegsPerSecond = Math.Cos(courseRadians) * speedDegsPerSecond;
            var velocityLatDegsPerSecond = Math.Sin(courseRadians) * speedDegsPerSecond;

            yield return new TrackState(index + 1, position, velocityLonDegsPerSecond, velocityLatDegsPerSecond);
        }
    }

    private double NextDouble(double min, double max)
    {
        return min + (_random.NextDouble() * (max - min));
    }
}

internal sealed class TrackState
{
    public TrackState(int id, LLPoint position, double velocityLonDegsPerSecond, double velocityLatDegsPerSecond)
    {
        Id = id;
        Position = position;
        VelocityLonDegsPerSecond = velocityLonDegsPerSecond;
        VelocityLatDegsPerSecond = velocityLatDegsPerSecond;
    }

    public int Id { get; }
    public LLPoint Position { get; private set; }
    public double VelocityLonDegsPerSecond { get; private set; }
    public double VelocityLatDegsPerSecond { get; private set; }
    public double CourseDegs => NormalizeDegrees(Math.Atan2(VelocityLatDegsPerSecond, VelocityLonDegsPerSecond) * (180d / Math.PI));
    public double SpeedDegsPerSecond => Math.Sqrt(
        (VelocityLonDegsPerSecond * VelocityLonDegsPerSecond) +
        (VelocityLatDegsPerSecond * VelocityLatDegsPerSecond));

    public void Advance(LLRect bounds, double deltaSeconds)
    {
        var nextLon = Position.LonDegs + (VelocityLonDegsPerSecond * deltaSeconds);
        var nextLat = Position.LatDegs + (VelocityLatDegsPerSecond * deltaSeconds);

        (nextLon, VelocityLonDegsPerSecond) = Reflect(nextLon, VelocityLonDegsPerSecond, bounds.LLonDegs, bounds.RLonDegs);
        (nextLat, VelocityLatDegsPerSecond) = Reflect(nextLat, VelocityLatDegsPerSecond, bounds.TLatDegs, bounds.BLatDegs);

        Position = new LLPoint(nextLon, nextLat);
    }

    private static (double Position, double Velocity) Reflect(double position, double velocity, double min, double max)
    {
        if (min == max)
        {
            return (min, 0d);
        }

        while (position < min || position > max)
        {
            if (position < min)
            {
                position = min + (min - position);
                velocity = -velocity;
            }
            else if (position > max)
            {
                position = max - (position - max);
                velocity = -velocity;
            }
        }

        return (position, velocity);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360d;
        if (normalized < 0d)
        {
            normalized += 360d;
        }

        return normalized;
    }
}

internal sealed record WorldSimOptions(
    LLRect Bounds,
    int TrackCount,
    TimeSpan TickInterval,
    double MinSpeedDegsPerSecond,
    double MaxSpeedDegsPerSecond)
{
    private static readonly LLRect DefaultBounds = new(-3d, 49.5d, 4d, 2d);
    private const int DefaultTrackCount = 50;
    private static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1d);
    private const double DefaultMinSpeedDegsPerSecond = 1d / 60d;
    private const double DefaultMaxSpeedDegsPerSecond = 1d / 60d;

    public static WorldSimOptions Parse(string[] args)
    {
        var values = ParseArgs(args);

        var topLeftLonDegs = GetDouble(values, "--lon", DefaultBounds.TLLonDegs);
        var topLeftLatDegs = GetDouble(values, "--lat", DefaultBounds.TLLatDegs);
        var widthDegs = GetDouble(values, "--width", DefaultBounds.WidthDegs);
        var heightDegs = GetDouble(values, "--height", DefaultBounds.HeightDegs);
        var bounds = new LLRect(topLeftLonDegs, topLeftLatDegs, widthDegs, heightDegs);
        var trackCount = GetInt(values, "--tracks", DefaultTrackCount);
        var tickMs = GetInt(values, "--interval-ms", (int)DefaultTickInterval.TotalMilliseconds);
        var minSpeedDegsPerSecond = GetDouble(values, "--min-speed-degs-per-sec", DefaultMinSpeedDegsPerSecond);
        var maxSpeedDegsPerSecond = GetDouble(values, "--max-speed-degs-per-sec", DefaultMaxSpeedDegsPerSecond);

        if (!bounds.IsValid())
        {
            throw new ArgumentOutOfRangeException(nameof(args), "The specified lat/lon box is not valid.");
        }

        if (trackCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Track count must be greater than zero.");
        }

        if (tickMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Tick interval must be greater than zero.");
        }

        if (minSpeedDegsPerSecond < 0d || maxSpeedDegsPerSecond <= 0d || minSpeedDegsPerSecond > maxSpeedDegsPerSecond)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Speed range must be non-negative and ordered.");
        }

        return new WorldSimOptions(
            bounds,
            trackCount,
            TimeSpan.FromMilliseconds(tickMs),
            minSpeedDegsPerSecond,
            maxSpeedDegsPerSecond);
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{key}'. Expected --name value pairs.", nameof(args));
            }

            if (index == args.Length - 1)
            {
                throw new ArgumentException($"Missing value for argument '{key}'.", nameof(args));
            }

            values[key] = args[index + 1];
            index++;
        }

        return values;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> values, string key, double defaultValue)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
