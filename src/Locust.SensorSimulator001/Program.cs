namespace Locust;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var options = SensorSimulatorOptions.Parse(args);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<SensorStateStore>();
        builder.Services.AddHostedService<SensorWorker>();
        builder.Services.AddHttpClient<LocustApiClient>(client =>
        {
            client.BaseAddress = new Uri(options.ApiBaseUrl, UriKind.Absolute);
        });
        builder.Services.AddHttpClient<WorldSimApiClient>(client =>
        {
            client.BaseAddress = new Uri(options.WorldSimBaseUrl, UriKind.Absolute);
        });

        var app = builder.Build();

        app.MapGet("/", (SensorSimulatorOptions config) => Results.Ok(new
        {
            service = "Locust.SensorSimulator001",
            worldSim = config.WorldSimBaseUrl,
            api = config.ApiBaseUrl,
            pollIntervalMs = config.PollInterval.TotalMilliseconds,
            endpoints = new[]
            {
                "GET /sensor",
                "PUT /sensor"
            }
        }));

        app.MapGet("/sensor", (SensorStateStore store) =>
        {
            return Results.Ok(store.GetResponse());
        });

        app.MapGet("/sensor/debug", (SensorStateStore store) =>
        {
            return Results.Ok(store.GetDebugResponse());
        });

        app.MapPut("/sensor", (UpdateSensorArcRequest request, SensorStateStore store) =>
        {
            try
            {
                store.Update(request.ToState());
                return Results.Ok(store.GetResponse());
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message, ex.ParamName));
            }
        });

        app.Run();
    }
}

internal sealed class SensorWorker : BackgroundService
{
    private readonly SensorSimulatorOptions _options;
    private readonly SensorStateStore _store;
    private readonly WorldSimApiClient _worldSimClient;
    private readonly LocustApiClient _locustApiClient;
    private readonly ILogger<SensorWorker> _logger;

    public SensorWorker(
        SensorSimulatorOptions options,
        SensorStateStore store,
        WorldSimApiClient worldSimClient,
        LocustApiClient locustApiClient,
        ILogger<SensorWorker> logger)
    {
        _options = options;
        _store = store;
        _worldSimClient = worldSimClient;
        _locustApiClient = locustApiClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Locust.SensorSimulator001 starting with world {WorldSim}, api {Api}, interval {IntervalMs}ms",
            _options.WorldSimBaseUrl,
            _options.ApiBaseUrl,
            _options.PollInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _worldSimClient.GetSnapshotAsync(stoppingToken);
                var sensor = _store.GetState();
                var visibleTracks = snapshot.Tracks.Where(track => IsVisible(sensor, track)).ToArray();
                _store.RecordObservation(snapshot.Tick, snapshot.Tracks.Count, visibleTracks);

                if (visibleTracks.Length > 0)
                {
                    var requests = visibleTracks
                        .Select(track => new RegisterPingRequest(
                            track.Position.LonDegs,
                            track.Position.LatDegs,
                            _options.PingStrength,
                            _options.PingRadiusDegs,
                            _options.PingDecaySecs))
                        .ToArray();

                    await _locustApiClient.RegisterPingsAsync(new RegisterPingsRequest(requests), stoppingToken);
                    _store.RecordPublishSuccess(requests, _options.PingRadiusDegs, _options.PingDecaySecs);
                }
                else
                {
                    _store.RecordPublishSuccess([], _options.PingRadiusDegs, _options.PingDecaySecs);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _store.RecordFailure(ex.Message);
                _logger.LogWarning(ex, "Sensor polling cycle failed.");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static bool IsVisible(SensorArcState sensor, WorldTrackResponse track)
    {
        var deltaLon = track.Position.LonDegs - sensor.Position.LonDegs;
        var deltaLat = track.Position.LatDegs - sensor.Position.LatDegs;
        var cosLat = Math.Cos(sensor.Position.LatDegs * (Math.PI / 180d));
        var localDeltaX = deltaLon * cosLat;
        var localDeltaY = deltaLat;
        var rangeDegs = Math.Sqrt((localDeltaX * localDeltaX) + (localDeltaY * localDeltaY));

        if (rangeDegs > sensor.RangeDegs)
        {
            return false;
        }

        if (localDeltaX == 0d && localDeltaY == 0d)
        {
            return true;
        }

        var bearingDegs = NormalizeDegrees(Math.Atan2(localDeltaY, localDeltaX) * (180d / Math.PI));
        var halfWidth = sensor.ArcWidthDegs / 2d;
        var delta = SmallestAngleDegrees(sensor.CenterDirectionDegs, bearingDegs);

        return Math.Abs(delta) <= halfWidth;
    }

    private static double SmallestAngleDegrees(double fromDegs, double toDegs)
    {
        var delta = NormalizeDegrees(toDegs - fromDegs);
        if (delta > 180d)
        {
            delta -= 360d;
        }

        return delta;
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

internal sealed class SensorStateStore
{
    private readonly object _sync = new();
    private SensorArcState _state;
    private int _lastObservedWorldTick;
    private int _lastObservedWorldTrackCount;
    private int _lastDetectedTrackCount;
    private int[] _lastVisibleTrackIds = [];
    private VisibleTrackDebugInfo[] _lastVisibleTracks = [];
    private int _lastPublishedPingCount;
    private double _lastPublishedPingRadiusDegs;
    private double _lastPublishedPingDecaySecs;
    private PublishedPingDebugInfo[] _lastPublishedPings = [];
    private bool _lastPublishSucceeded;
    private string? _lastError;
    private DateTimeOffset _updatedAtUtc;

    public SensorStateStore(SensorSimulatorOptions options)
    {
        _state = new SensorArcState(options.Position, options.CenterDirectionDegs, options.ArcWidthDegs, options.RangeDegs);
        _updatedAtUtc = DateTimeOffset.UtcNow;
    }

    public SensorArcState GetState()
    {
        lock (_sync)
        {
            return _state;
        }
    }

    public SensorArcResponse GetResponse()
    {
        lock (_sync)
        {
            return new SensorArcResponse(
                _state.Position,
                _state.CenterDirectionDegs,
                _state.ArcWidthDegs,
                _state.RangeDegs,
                _lastObservedWorldTick,
                _lastDetectedTrackCount,
                _updatedAtUtc);
        }
    }

    public object GetDebugResponse()
    {
        lock (_sync)
        {
            return new
            {
                Position = _state.Position,
                CenterDirectionDegs = _state.CenterDirectionDegs,
                ArcWidthDegs = _state.ArcWidthDegs,
                RangeDegs = _state.RangeDegs,
                LastObservedWorldTick = _lastObservedWorldTick,
                LastObservedWorldTrackCount = _lastObservedWorldTrackCount,
                LastDetectedTrackCount = _lastDetectedTrackCount,
                LastVisibleTrackIds = _lastVisibleTrackIds,
                LastVisibleTracks = _lastVisibleTracks,
                LastPublishedPingCount = _lastPublishedPingCount,
                LastPublishedPingRadiusDegs = _lastPublishedPingRadiusDegs,
                LastPublishedPingDecaySecs = _lastPublishedPingDecaySecs,
                LastPublishedPings = _lastPublishedPings,
                LastPublishSucceeded = _lastPublishSucceeded,
                LastError = _lastError,
                UpdatedAtUtc = _updatedAtUtc
            };
        }
    }

    public void Update(SensorArcState state)
    {
        Validate(state);

        lock (_sync)
        {
            _state = new SensorArcState(
                new LLPoint(state.Position.LonDegs, state.Position.LatDegs),
                NormalizeDegrees(state.CenterDirectionDegs),
                state.ArcWidthDegs,
                state.RangeDegs);
            _updatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordObservation(int worldTick, int worldTrackCount, IReadOnlyList<WorldTrackResponse> visibleTracks)
    {
        lock (_sync)
        {
            _lastObservedWorldTick = worldTick;
            _lastObservedWorldTrackCount = worldTrackCount;
            _lastDetectedTrackCount = visibleTracks.Count;
            _lastVisibleTrackIds = visibleTracks.Select(track => track.Id).ToArray();
            _lastVisibleTracks = visibleTracks
                .Select(track => new VisibleTrackDebugInfo(
                    track.Id,
                    track.Position,
                    track.CourseDegs,
                    track.SpeedDegsPerSecond))
                .ToArray();
            _lastError = null;
            _updatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordPublishSuccess(IReadOnlyList<RegisterPingRequest> publishedPings, double pingRadiusDegs, double pingDecaySecs)
    {
        lock (_sync)
        {
            _lastPublishedPingCount = publishedPings.Count;
            _lastPublishedPingRadiusDegs = pingRadiusDegs;
            _lastPublishedPingDecaySecs = pingDecaySecs;
            _lastPublishedPings = publishedPings
                .Select(ping => new PublishedPingDebugInfo(
                    new LLPoint(ping.LonDegs, ping.LatDegs),
                    ping.Strength,
                    ping.RadiusDegs,
                    ping.DecaySecs))
                .ToArray();
            _lastPublishSucceeded = true;
            _lastError = null;
            _updatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordFailure(string error)
    {
        lock (_sync)
        {
            _lastPublishSucceeded = false;
            _lastError = error;
            _updatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void Validate(SensorArcState state)
    {
        if (state.Position.LonDegs < -180d || state.Position.LonDegs > 180d)
        {
            throw new ArgumentOutOfRangeException(nameof(state.Position.LonDegs), "Sensor longitude must be between -180 and 180 degrees.");
        }

        if (state.Position.LatDegs < -90d || state.Position.LatDegs > 90d)
        {
            throw new ArgumentOutOfRangeException(nameof(state.Position.LatDegs), "Sensor latitude must be between -90 and 90 degrees.");
        }

        if (state.ArcWidthDegs <= 0d || state.ArcWidthDegs > 360d)
        {
            throw new ArgumentOutOfRangeException(nameof(state.ArcWidthDegs), "Sensor arc width must be greater than 0 and at most 360 degrees.");
        }

        if (state.RangeDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(state.RangeDegs), "Sensor range must be greater than zero.");
        }
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

internal sealed record VisibleTrackDebugInfo(
    int Id,
    LLPoint Position,
    double CourseDegs,
    double SpeedDegsPerSecond);

internal sealed record PublishedPingDebugInfo(
    LLPoint Position,
    double Strength,
    double RadiusDegs,
    double DecaySecs);

internal sealed record SensorSimulatorOptions(
    string WorldSimBaseUrl,
    string ApiBaseUrl,
    LLPoint Position,
    double CenterDirectionDegs,
    double ArcWidthDegs,
    double RangeDegs,
    TimeSpan PollInterval,
    double PingStrength,
    double PingRadiusDegs,
    double PingDecaySecs)
{
    private const double ApproxMetersPerDegree = 111_320d;
    private const string DefaultWorldSimBaseUrl = "http://localhost:5100";
    private const string DefaultApiBaseUrl = "http://localhost:5000";
    private static readonly LLPoint DefaultPosition = new(-1.082d, 50.68d);
    private const double DefaultCenterDirectionDegs = 45d;
    private const double DefaultArcWidthDegs = 60d;
    private const double DefaultRangeDegs = 0.75d;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1d);
    private const double DefaultPingStrength = 10d;
    private const double DefaultPingRadiusDegs = 50d / ApproxMetersPerDegree;
    private const double DefaultPingDecaySecs = 8d;

    public static SensorSimulatorOptions Parse(string[] args)
    {
        var values = ParseArgs(args);

        var worldSimBaseUrl = GetString(values, "--world", DefaultWorldSimBaseUrl);
        var apiBaseUrl = GetString(values, "--api", DefaultApiBaseUrl);
        var lonDegs = GetDouble(values, "--lon", DefaultPosition.LonDegs);
        var latDegs = GetDouble(values, "--lat", DefaultPosition.LatDegs);
        var centerDirectionDegs = GetDouble(values, "--direction-degs", DefaultCenterDirectionDegs);
        var arcWidthDegs = GetDouble(values, "--arc-width-degs", DefaultArcWidthDegs);
        var rangeDegs = GetDouble(values, "--range-degs", DefaultRangeDegs);
        var intervalMs = GetInt(values, "--interval-ms", (int)DefaultPollInterval.TotalMilliseconds);
        var pingStrength = GetDouble(values, "--strength", DefaultPingStrength);
        var pingRadiusDegs = GetDouble(values, "--radius-degs", DefaultPingRadiusDegs);
        var pingDecaySecs = GetDouble(values, "--decay-secs", DefaultPingDecaySecs);

        if (lonDegs < -180d || lonDegs > 180d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Sensor longitude must be between -180 and 180 degrees.");
        }

        if (latDegs < -90d || latDegs > 90d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Sensor latitude must be between -90 and 90 degrees.");
        }

        if (arcWidthDegs <= 0d || arcWidthDegs > 360d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Sensor arc width must be greater than 0 and at most 360 degrees.");
        }

        if (rangeDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Sensor range must be greater than zero.");
        }

        if (intervalMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Poll interval must be greater than zero.");
        }

        if (pingRadiusDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Ping radius must be greater than zero.");
        }

        if (pingDecaySecs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Ping decay seconds must be greater than zero.");
        }

        return new SensorSimulatorOptions(
            worldSimBaseUrl,
            apiBaseUrl,
            new LLPoint(lonDegs, latDegs),
            NormalizeDegrees(centerDirectionDegs),
            arcWidthDegs,
            rangeDegs,
            TimeSpan.FromMilliseconds(intervalMs),
            pingStrength,
            pingRadiusDegs,
            pingDecaySecs);
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

    private static string GetString(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) ? value : defaultValue;
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
