namespace Locust;

public static class Program
{
    private const string DefaultApiBaseUrl = "http://localhost:5000";
    private static readonly LLPoint DefaultCenter = new(-0.1276d, 51.5072d);
    private const double Level7CellWidthDegs = 360d / 128d;
    private const double Level7CellHeightDegs = 180d / 128d;
    private const double DefaultStrength = 12d;
    private const double DefaultRadiusDegs = Level7CellWidthDegs;
    private const double DefaultDecaySecs = 20d;
    private const double DefaultNoiseFraction = 0.15d;
    private const double DefaultLonWanderDegs = Level7CellWidthDegs * 2.5d;
    private const double DefaultLatWanderDegs = Level7CellHeightDegs * 2.5d;

    public static async Task Main(string[] args)
    {
        var apiBaseUrl = args.Length > 0 ? args[0] : DefaultApiBaseUrl;

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
        };

        var apiClient = new LocustApiClient(httpClient);
        var random = new Random();
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.WriteLine($"Locust.InputSimulator -> {apiBaseUrl}");
        Console.WriteLine($"Pinging around Lon {DefaultCenter.LonDegs}, Lat {DefaultCenter.LatDegs} once per second with level-7 scale position drift. Press Ctrl+C to stop.");

        var tick = 0;

        while (!cancellation.IsCancellationRequested)
        {
            tick++;

            var strengthNoise = 1d + ((random.NextDouble() * 2d) - 1d) * DefaultNoiseFraction;
            var phase = tick / 12d;
            var lonNoise =
                (Math.Sin(phase) * DefaultLonWanderDegs) +
                (((random.NextDouble() * 2d) - 1d) * Level7CellWidthDegs * 0.75d);
            var latNoise =
                (Math.Cos(phase * 0.7d) * DefaultLatWanderDegs) +
                (((random.NextDouble() * 2d) - 1d) * Level7CellHeightDegs * 0.75d);

            var request = new RegisterPingRequest(
                DefaultCenter.LonDegs + lonNoise,
                DefaultCenter.LatDegs + latNoise,
                DefaultStrength * strengthNoise,
                DefaultRadiusDegs,
                DefaultDecaySecs);

            try
            {
                var response = await apiClient.RegisterPingAsync(request, cancellation.Token);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} ping -> center {response.Center}, strength {response.Strength:F3}, node {response.NodeBounds}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} ping failed -> {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1d), cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
