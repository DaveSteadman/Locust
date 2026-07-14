namespace Locust;

public static class Program
{
    private const string DefaultApiBaseUrl = "http://localhost:5000";
    private const int Level = 7;
    private const int CellCount = 1 << Level;
    private const int CellPixelWidth = 8;
    private const int CellPixelHeight = 4;
    private const string DefaultOutputFileName = "heatmap_level7.png";

    public static async Task Main(string[] args)
    {
        var apiBaseUrl = args.Length > 0 ? args[0] : DefaultApiBaseUrl;
        var outputPath = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, DefaultOutputFileName);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
        };

        var apiClient = new LocustApiClient(httpClient);
        var centerPosition = QuadTreePosition.FromGridCoordinates(Level, CellCount / 2, CellCount / 2);

        Console.WriteLine($"Locust.Heatmap -> {apiBaseUrl}");
        Console.WriteLine($"Querying full world at level {Level} ({CellCount}x{CellCount})...");

        var response = await apiClient.QueryRectAsync(
            new TreeRectQueryRequest(centerPosition.ToString(), CellCount, CellCount));

        RenderHeatmap(response, outputPath);
        Console.WriteLine($"Heatmap written to {outputPath}");
    }

    private static void RenderHeatmap(TreeRectQueryResponse response, string outputPath)
    {
        var width = response.WidthCount;
        var height = response.HeightCount;
        var bitmapWidth = width * CellPixelWidth;
        var bitmapHeight = height * CellPixelHeight;

        var maxStrength = 0d;
        foreach (var row in response.Nodes)
        {
            foreach (var cell in row)
            {
                if (cell.IsInsideTree && cell.Strength > maxStrength)
                {
                    maxStrength = cell.Strength;
                }
            }
        }

        if (maxStrength <= 0d)
        {
            maxStrength = 1d;
        }

        using var bitmap = new SkiaSharp.SKBitmap(bitmapWidth, bitmapHeight);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(new SkiaSharp.SKColor(7, 10, 18));

        using var paint = new SkiaSharp.SKPaint { IsAntialias = false, Style = SkiaSharp.SKPaintStyle.Fill };

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = response.Nodes[y][x];
                paint.Color = GetHeatColor(cell, maxStrength);

                canvas.DrawRect(
                    x * CellPixelWidth,
                    y * CellPixelHeight,
                    CellPixelWidth,
                    CellPixelHeight,
                    paint);
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static SkiaSharp.SKColor GetHeatColor(TreeRectCellResponse cell, double maxStrength)
    {
        if (!cell.IsInsideTree)
        {
            return new SkiaSharp.SKColor(4, 6, 10);
        }

        var normalized = Math.Clamp(cell.Strength / maxStrength, 0d, 1d);
        normalized = Math.Sqrt(normalized);

        var red = (byte)Math.Round(255d * normalized);
        var green = (byte)Math.Round(220d * Math.Pow(normalized, 1.4d));
        var blue = (byte)Math.Round(80d * (1d - normalized));

        if (normalized < 0.001d)
        {
            return new SkiaSharp.SKColor(14, 18, 28);
        }

        return new SkiaSharp.SKColor(red, green, blue);
    }
}
