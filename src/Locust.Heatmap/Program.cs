using System.Text.Json;

namespace Locust;

public static class Program
{
    private const int DebugTreeLevel = 13;
    private const int DebugTreeRadiusCells = 50;
    private const string DefaultApiBaseUrl = "http://localhost:5000";
    private const string DefaultWorldSimBaseUrl = "http://localhost:5100";
    private static readonly string[] DefaultSensorBaseUrls = ["http://localhost:5200"];
    private static readonly string DefaultUkOutlinePath = Path.Combine(Environment.CurrentDirectory, "data", "CountryOutline_UK.geojson");
    private const int GridWidthCount = 128;
    private const int GridHeightCount = 128;
    private const int BaseCellPixelSize = 6;
    private const string DefaultOutputFileName = "heatmap_level7.png";
    private const double ZoomOutFactor = 1.5d;
    private static readonly TimeSpan DefaultRenderInterval = TimeSpan.Zero;

    public static async Task Main(string[] args)
    {
        var (apiBaseUrl, worldSimBaseUrl, sensorBaseUrls, outputPath, renderInterval) = ParseArgs(args);

        using var apiHttpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
        };
        using var worldSimHttpClient = new HttpClient
        {
            BaseAddress = new Uri(worldSimBaseUrl, UriKind.Absolute)
        };
        var apiClient = new LocustApiClient(apiHttpClient);
        var worldSimClient = new WorldSimApiClient(worldSimHttpClient);
        var sensorClients = sensorBaseUrls
            .Select(sensorBaseUrl => new SensorSimulatorApiClient(new HttpClient
            {
                BaseAddress = new Uri(sensorBaseUrl, UriKind.Absolute)
            }))
            .ToArray();

        Console.WriteLine($"Locust.Heatmap -> {apiBaseUrl}");
        Console.WriteLine($"Locust.WorldSim -> {worldSimBaseUrl}");
        foreach (var sensorBaseUrl in sensorBaseUrls)
        {
            Console.WriteLine($"Locust.SensorSimulator001 -> {sensorBaseUrl}");
        }
        var ukOutline = GeoJsonMapOverlay.Load(DefaultUkOutlinePath);
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.WriteLine($"Querying regional heatmap at {GridWidthCount}x{GridHeightCount} around the WorldSim box...");

        var frame = 0;
        var lastDebugExtract = new DebugExtractOverlay(LLRect.Zero, 0d, []);
        do
        {
            frame++;

            var worldSnapshot = await worldSimClient.GetSnapshotAsync(cancellation.Token);
            var sensors = await Task.WhenAll(sensorClients.Select(client => client.GetSensorAsync(cancellation.Token)));
            var viewBounds = CreateZoomBounds(worldSnapshot.Bounds, ZoomOutFactor);
            var debugExtract = await TryQueryDebugExtractAsync(
                apiClient,
                worldSnapshot.Bounds,
                lastDebugExtract,
                cancellation.Token);
            lastDebugExtract = debugExtract;

            RenderHeatmap(viewBounds, worldSnapshot, sensors, ukOutline, debugExtract, outputPath);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} frame {frame,4} -> {outputPath}");

            if (renderInterval <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                await Task.Delay(renderInterval, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
        }
        while (!cancellation.IsCancellationRequested);
    }

    private static async Task<DebugExtractOverlay> TryQueryDebugExtractAsync(
        LocustApiClient apiClient,
        LLRect worldBounds,
        DebugExtractOverlay fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            return await QueryDebugExtractAsync(apiClient, worldBounds, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: using previous heat extract after query failure: {ex.Message}");
            return fallback;
        }
    }

    private static void RenderHeatmap(
        LLRect viewBounds,
        WorldSnapshotResponse worldSnapshot,
        IReadOnlyList<SensorArcResponse> sensors,
        GeoJsonMapOverlay ukOutline,
        DebugExtractOverlay debugExtract,
        string outputPath)
    {
        var (bitmapWidth, bitmapHeight) = CalculateBitmapSize(viewBounds, GridWidthCount, GridHeightCount);

        using var bitmap = new SkiaSharp.SKBitmap(bitmapWidth, bitmapHeight);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(new SkiaSharp.SKColor(7, 10, 18));

        DrawGeoJsonOverlay(canvas, ukOutline, viewBounds, bitmapWidth, bitmapHeight);
        DrawWorldBoundsOverlay(canvas, worldSnapshot.Bounds, viewBounds, bitmapWidth, bitmapHeight);
        DrawTruthTrackOverlay(canvas, worldSnapshot.Tracks, viewBounds, bitmapWidth, bitmapHeight);
        DrawHeatCellsOverlay(canvas, debugExtract, viewBounds, bitmapWidth, bitmapHeight);
        DrawSensorOverlays(canvas, sensors, viewBounds, bitmapWidth, bitmapHeight);
        DrawExtractBoundsOverlay(canvas, debugExtract.Bounds, viewBounds, bitmapWidth, bitmapHeight);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static async Task<DebugExtractOverlay> QueryDebugExtractAsync(
        LocustApiClient apiClient,
        LLRect worldBounds,
        CancellationToken cancellationToken)
    {
        var centerPoint = new LLPoint(worldBounds.MidLonDegs, worldBounds.MidLatDegs);
        var centerPosition = GetPositionAtDepth(centerPoint, DebugTreeLevel);
        var cellCount = (DebugTreeRadiusCells * 2) + 1;
        var response = await apiClient.QueryRectAsync(
            new TreeRectQueryRequest(centerPosition.ToString(), cellCount, cellCount),
            cancellationToken);

        var cells = new List<DebugExtractCell>();
        var minLon = double.PositiveInfinity;
        var maxLon = double.NegativeInfinity;
        var minLat = double.PositiveInfinity;
        var maxLat = double.NegativeInfinity;
        var maxStrength = 0d;

        foreach (var row in response.Nodes)
        {
            foreach (var cell in row)
            {
                if (!cell.IsInsideTree)
                {
                    continue;
                }

                if (cell.Strength > 0d)
                {
                    cells.Add(new DebugExtractCell(cell.Bounds, cell.Strength));
                }

                minLon = Math.Min(minLon, cell.Bounds.LLonDegs);
                maxLon = Math.Max(maxLon, cell.Bounds.RLonDegs);
                minLat = Math.Min(minLat, cell.Bounds.TLatDegs);
                maxLat = Math.Max(maxLat, cell.Bounds.BLatDegs);
                maxStrength = Math.Max(maxStrength, cell.Strength);
            }
        }

        var bounds = double.IsInfinity(minLon)
            ? LLRect.Zero
            : new LLRect(minLon, minLat, maxLon - minLon, maxLat - minLat);

        return new DebugExtractOverlay(bounds, maxStrength, cells);
    }

    private static void DrawHeatCellsOverlay(
        SkiaSharp.SKCanvas canvas,
        DebugExtractOverlay extract,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        if (extract.Cells.Count == 0)
        {
            return;
        }

        var maxStrength = extract.MaxStrength <= 0d ? 1d : extract.MaxStrength;
        using var paint = new SkiaSharp.SKPaint
        {
            IsAntialias = false,
            Style = SkiaSharp.SKPaintStyle.Fill
        };
        using var borderPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = false,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SkiaSharp.SKColor(255, 245, 180, 220)
        };

        foreach (var cell in extract.Cells)
        {
            paint.Color = GetHeatColor(cell.Strength, maxStrength);

            var left = ProjectLonToPixel(cell.Bounds.LLonDegs, viewBounds, bitmapWidth);
            var right = ProjectLonToPixel(cell.Bounds.RLonDegs, viewBounds, bitmapWidth);
            var northLat = Math.Max(cell.Bounds.TLatDegs, cell.Bounds.BLatDegs);
            var southLat = Math.Min(cell.Bounds.TLatDegs, cell.Bounds.BLatDegs);
            var top = ProjectLatToPixel(northLat, viewBounds, bitmapHeight);
            var bottom = ProjectLatToPixel(southLat, viewBounds, bitmapHeight);

            var rect = SkiaSharp.SKRect.Create(
                left,
                top,
                Math.Max(1f, right - left),
                Math.Max(1f, bottom - top));
            canvas.DrawRect(rect, paint);
            canvas.DrawRect(rect, borderPaint);
        }
    }

    private static void DrawExtractBoundsOverlay(
        SkiaSharp.SKCanvas canvas,
        LLRect extractBounds,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        if (extractBounds == LLRect.Zero)
        {
            return;
        }

        var left = ProjectLonToPixel(extractBounds.LLonDegs, viewBounds, bitmapWidth);
        var right = ProjectLonToPixel(extractBounds.RLonDegs, viewBounds, bitmapWidth);
        var northLat = Math.Max(extractBounds.TLatDegs, extractBounds.BLatDegs);
        var southLat = Math.Min(extractBounds.TLatDegs, extractBounds.BLatDegs);
        var top = ProjectLatToPixel(northLat, viewBounds, bitmapHeight);
        var bottom = ProjectLatToPixel(southLat, viewBounds, bitmapHeight);

        using var borderPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = new SkiaSharp.SKColor(255, 120, 120)
        };
        using var fillPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill,
            Color = new SkiaSharp.SKColor(255, 120, 120, 20)
        };

        var rect = SkiaSharp.SKRect.Create(left, top, Math.Max(1f, right - left), Math.Max(1f, bottom - top));
        canvas.DrawRect(rect, fillPaint);
        canvas.DrawRect(rect, borderPaint);
    }

    private static void DrawTruthTrackOverlay(
        SkiaSharp.SKCanvas canvas,
        IReadOnlyList<WorldTrackResponse> tracks,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        using var dotPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill,
            Color = new SkiaSharp.SKColor(80, 255, 120)
        };
        using var haloPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SkiaSharp.SKColor(10, 40, 16, 220)
        };

        foreach (var track in tracks)
        {
            if (!viewBounds.Contains(track.Position))
            {
                continue;
            }

            var x = ProjectLonToPixel(track.Position.LonDegs, viewBounds, bitmapWidth);
            var y = ProjectLatToPixel(track.Position.LatDegs, viewBounds, bitmapHeight);

            canvas.DrawCircle(x, y, 4f, dotPaint);
            canvas.DrawCircle(x, y, 5.5f, haloPaint);
        }
    }

    private static void DrawGeoJsonOverlay(
        SkiaSharp.SKCanvas canvas,
        GeoJsonMapOverlay overlay,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        using var fillPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill,
            Color = new SkiaSharp.SKColor(40, 70, 92, 96)
        };
        using var strokePaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SkiaSharp.SKColor(125, 175, 205, 180)
        };

        foreach (var polygon in overlay.Polygons)
        {
            if (!polygon.Bounds.Intersects(viewBounds))
            {
                continue;
            }

            var builder = new SkiaSharp.SKPathBuilder();
            AddRing(builder, polygon.OuterRing, viewBounds, bitmapWidth, bitmapHeight);

            foreach (var innerRing in polygon.InnerRings)
            {
                AddRing(builder, innerRing, viewBounds, bitmapWidth, bitmapHeight);
            }

            using var path = builder.Detach();
            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);
        }
    }

    private static void AddRing(
        SkiaSharp.SKPathBuilder builder,
        IReadOnlyList<LLPoint> ring,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        if (ring.Count < 3)
        {
            return;
        }

        builder.MoveTo(
            ProjectLonToPixel(ring[0].LonDegs, viewBounds, bitmapWidth),
            ProjectLatToPixel(ring[0].LatDegs, viewBounds, bitmapHeight));

        for (var index = 1; index < ring.Count; index++)
        {
            builder.LineTo(
                ProjectLonToPixel(ring[index].LonDegs, viewBounds, bitmapWidth),
                ProjectLatToPixel(ring[index].LatDegs, viewBounds, bitmapHeight));
        }

        builder.Close();
    }

    private static (int BitmapWidth, int BitmapHeight) CalculateBitmapSize(LLRect viewBounds, int gridWidth, int gridHeight)
    {
        var centerLatRadians = viewBounds.MidLatDegs * (Math.PI / 180d);
        var localAspect = (viewBounds.WidthDegs * Math.Cos(centerLatRadians)) / viewBounds.HeightDegs;
        var safeAspect = Math.Max(0.1d, localAspect);

        var bitmapHeight = gridHeight * BaseCellPixelSize;
        var bitmapWidth = Math.Max(1, (int)Math.Round(bitmapHeight * safeAspect));

        return (bitmapWidth, bitmapHeight);
    }

    private static void DrawWorldBoundsOverlay(
        SkiaSharp.SKCanvas canvas,
        LLRect worldBounds,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        var left = ProjectLonToPixel(worldBounds.LLonDegs, viewBounds, bitmapWidth);
        var right = ProjectLonToPixel(worldBounds.RLonDegs, viewBounds, bitmapWidth);
        var northLat = Math.Max(worldBounds.TLatDegs, worldBounds.BLatDegs);
        var southLat = Math.Min(worldBounds.TLatDegs, worldBounds.BLatDegs);
        var top = ProjectLatToPixel(northLat, viewBounds, bitmapHeight);
        var bottom = ProjectLatToPixel(southLat, viewBounds, bitmapHeight);

        var rect = SkiaSharp.SKRect.Create(left, top, Math.Max(1f, right - left), Math.Max(1f, bottom - top));

        using var glowPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 6f,
            Color = new SkiaSharp.SKColor(0, 0, 0, 120)
        };
        using var outlinePaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = new SkiaSharp.SKColor(80, 255, 120)
        };

        canvas.DrawRect(rect, glowPaint);
        canvas.DrawRect(rect, outlinePaint);
    }

    private static void DrawSensorOverlays(
        SkiaSharp.SKCanvas canvas,
        IReadOnlyList<SensorArcResponse> sensors,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        foreach (var sensor in sensors)
        {
            DrawSensorOverlay(canvas, sensor, viewBounds, bitmapWidth, bitmapHeight);
        }
    }

    private static void DrawSensorOverlay(
        SkiaSharp.SKCanvas canvas,
        SensorArcResponse sensor,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        using var fillPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill,
            Color = new SkiaSharp.SKColor(255, 210, 80, 48)
        };
        using var outlinePaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = new SkiaSharp.SKColor(255, 225, 120)
        };
        using var sensorPaint = new SkiaSharp.SKPaint
        {
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill,
            Color = new SkiaSharp.SKColor(255, 245, 180)
        };

        var centerX = ProjectLonToPixel(sensor.Position.LonDegs, viewBounds, bitmapWidth);
        var centerY = ProjectLatToPixel(sensor.Position.LatDegs, viewBounds, bitmapHeight);
        using var path = CreateSectorPath(sensor, viewBounds, bitmapWidth, bitmapHeight);

        canvas.DrawPath(path, fillPaint);
        canvas.DrawPath(path, outlinePaint);
        canvas.DrawCircle(centerX, centerY, 4f, sensorPaint);
    }

    private static SkiaSharp.SKPath CreateSectorPath(
        SensorArcResponse sensor,
        LLRect viewBounds,
        int bitmapWidth,
        int bitmapHeight)
    {
        var centerX = ProjectLonToPixel(sensor.Position.LonDegs, viewBounds, bitmapWidth);
        var centerY = ProjectLatToPixel(sensor.Position.LatDegs, viewBounds, bitmapHeight);
        var builder = new SkiaSharp.SKPathBuilder();
        builder.MoveTo(centerX, centerY);

        var startDegs = sensor.CenterDirectionDegs - (sensor.ArcWidthDegs / 2d);
        var endDegs = sensor.CenterDirectionDegs + (sensor.ArcWidthDegs / 2d);
        var steps = Math.Max(12, (int)Math.Ceiling(sensor.ArcWidthDegs / 4d));

        for (var step = 0; step <= steps; step++)
        {
            var t = (double)step / steps;
            var angleDegs = startDegs + ((endDegs - startDegs) * t);
            var angleRadians = angleDegs * (Math.PI / 180d);
            var point = ProjectSensorRangePoint(sensor.Position, sensor.RangeDegs, angleRadians);
            var x = ProjectLonToPixel(point.LonDegs, viewBounds, bitmapWidth);
            var y = ProjectLatToPixel(point.LatDegs, viewBounds, bitmapHeight);
            builder.LineTo(x, y);
        }

        builder.Close();
        return builder.Detach();
    }

    private static LLPoint ProjectSensorRangePoint(LLPoint origin, double rangeDegs, double angleRadians)
    {
        var deltaLat = Math.Sin(angleRadians) * rangeDegs;
        var cosLat = Math.Cos(origin.LatDegs * (Math.PI / 180d));
        var safeCosLat = Math.Abs(cosLat) < 1e-9 ? 1e-9 : cosLat;
        var deltaLon = (Math.Cos(angleRadians) * rangeDegs) / safeCosLat;

        return new LLPoint(origin.LonDegs + deltaLon, origin.LatDegs + deltaLat);
    }

    private static QuadTreePosition GetPositionAtDepth(LLPoint point, int depth)
    {
        var radiusDegs = 360d / (1 << depth);
        return QuadTreePosition.FromPointRadius(point, radiusDegs);
    }

    private static LLRect CreateZoomBounds(LLRect bounds, double zoomOutFactor)
    {
        var width = Math.Min(360d, bounds.WidthDegs * zoomOutFactor);
        var height = Math.Min(180d, bounds.HeightDegs * zoomOutFactor);
        var lon = bounds.MidLonDegs - (width / 2d);
        var lat = bounds.MidLatDegs - (height / 2d);

        lon = Math.Clamp(lon, -180d, 180d - width);
        lat = Math.Clamp(lat, -90d, 90d - height);

        return new LLRect(lon, lat, width, height);
    }

    private static float ProjectLonToPixel(double lonDegs, LLRect viewBounds, int bitmapWidth)
    {
        var normalized = (lonDegs - viewBounds.LLonDegs) / viewBounds.WidthDegs;
        return (float)Math.Clamp(normalized * bitmapWidth, 0d, bitmapWidth - 1d);
    }

    private static float ProjectLatToPixel(double latDegs, LLRect viewBounds, int bitmapHeight)
    {
        var normalized = (viewBounds.BLatDegs - latDegs) / viewBounds.HeightDegs;
        return (float)Math.Clamp(normalized * bitmapHeight, 0d, bitmapHeight - 1d);
    }

    private static (string ApiBaseUrl, string WorldSimBaseUrl, string[] SensorBaseUrls, string OutputPath, TimeSpan RenderInterval) ParseArgs(string[] args)
    {
        var apiBaseUrl = DefaultApiBaseUrl;
        var worldSimBaseUrl = DefaultWorldSimBaseUrl;
        var sensorBaseUrls = DefaultSensorBaseUrls;
        var outputPath = Path.Combine(Environment.CurrentDirectory, DefaultOutputFileName);
        var renderInterval = DefaultRenderInterval;

        if (args.Length > 0)
        {
            apiBaseUrl = args[0];
        }

        if (args.Length > 1)
        {
            if (Uri.IsWellFormedUriString(args[1], UriKind.Absolute))
            {
                worldSimBaseUrl = args[1];
            }
            else
            {
                outputPath = args[1];
            }
        }

        if (args.Length > 2)
        {
            var sensorUrlCandidates = args[2]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (sensorUrlCandidates.Length > 0 &&
                sensorUrlCandidates.All(candidate => Uri.IsWellFormedUriString(candidate, UriKind.Absolute)))
            {
                sensorBaseUrls = sensorUrlCandidates;
            }
            else
            {
                outputPath = args[2];
            }
        }

        if (args.Length > 3)
        {
            outputPath = args[3];
        }

        if (args.Length > 4)
        {
            renderInterval = TimeSpan.FromMilliseconds(
                double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture));
        }

        return (apiBaseUrl, worldSimBaseUrl, sensorBaseUrls, outputPath, renderInterval);
    }

    private static SkiaSharp.SKColor GetHeatColor(double strength, double maxStrength)
    {
        var normalized = Math.Clamp(strength / maxStrength, 0d, 1d);
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

internal sealed class GeoJsonMapOverlay
{
    public GeoJsonMapOverlay(IReadOnlyList<GeoPolygon> polygons)
    {
        Polygons = polygons;
    }

    public IReadOnlyList<GeoPolygon> Polygons { get; }

    public static GeoJsonMapOverlay Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var polygons = new List<GeoPolygon>();

        var root = document.RootElement;
        if (!root.TryGetProperty("features", out var features))
        {
            throw new InvalidOperationException($"GeoJSON file '{path}' does not contain a features array.");
        }

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("geometry", out var geometry))
            {
                continue;
            }

            if (!geometry.TryGetProperty("type", out var typeElement) ||
                !geometry.TryGetProperty("coordinates", out var coordinatesElement))
            {
                continue;
            }

            var geometryType = typeElement.GetString();
            if (string.Equals(geometryType, "Polygon", StringComparison.OrdinalIgnoreCase))
            {
                polygons.Add(ParsePolygon(coordinatesElement));
            }
            else if (string.Equals(geometryType, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var polygonElement in coordinatesElement.EnumerateArray())
                {
                    polygons.Add(ParsePolygon(polygonElement));
                }
            }
        }

        return new GeoJsonMapOverlay(polygons);
    }

    private static GeoPolygon ParsePolygon(JsonElement polygonElement)
    {
        var rings = new List<IReadOnlyList<LLPoint>>();
        foreach (var ringElement in polygonElement.EnumerateArray())
        {
            var ring = new List<LLPoint>();
            foreach (var pointElement in ringElement.EnumerateArray())
            {
                if (pointElement.GetArrayLength() < 2)
                {
                    continue;
                }

                var lon = pointElement[0].GetDouble();
                var lat = pointElement[1].GetDouble();
                ring.Add(new LLPoint(lon, lat));
            }

            if (ring.Count >= 3)
            {
                rings.Add(ring);
            }
        }

        if (rings.Count == 0)
        {
            throw new InvalidOperationException("Encountered a polygon without any valid rings.");
        }

        var bounds = ComputeBounds(rings[0]);
        return new GeoPolygon(bounds, rings[0], rings.Skip(1).ToArray());
    }

    private static LLRect ComputeBounds(IReadOnlyList<LLPoint> points)
    {
        var minLon = double.PositiveInfinity;
        var maxLon = double.NegativeInfinity;
        var minLat = double.PositiveInfinity;
        var maxLat = double.NegativeInfinity;

        foreach (var point in points)
        {
            minLon = Math.Min(minLon, point.LonDegs);
            maxLon = Math.Max(maxLon, point.LonDegs);
            minLat = Math.Min(minLat, point.LatDegs);
            maxLat = Math.Max(maxLat, point.LatDegs);
        }

        return new LLRect(minLon, minLat, maxLon - minLon, maxLat - minLat);
    }
}

internal sealed record GeoPolygon(
    LLRect Bounds,
    IReadOnlyList<LLPoint> OuterRing,
    IReadOnlyList<IReadOnlyList<LLPoint>> InnerRings);

internal sealed record DebugExtractOverlay(
    LLRect Bounds,
    double MaxStrength,
    IReadOnlyList<DebugExtractCell> Cells);

internal sealed record DebugExtractCell(
    LLRect Bounds,
    double Strength);
