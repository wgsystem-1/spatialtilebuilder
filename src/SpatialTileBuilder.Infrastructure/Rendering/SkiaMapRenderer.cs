namespace SpatialTileBuilder.Infrastructure.Rendering;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using SkiaSharp;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using SpatialTileBuilder.Infrastructure.Data;
using NetTopologySuite.Geometries;

public class SkiaMapRenderer : IMapRenderer
{
    private readonly LayerDataProviderFactory _providerFactory;
    
    // Cache: LayerId -> Geometries
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<Geometry>> _geometryCache = new();
    
    // Store latest project state for Draw to use (styles)
    private ProjectConfiguration? _lastProject;
    private double _lastCx, _lastCy, _lastZoom;

    public SkiaMapRenderer(LayerDataProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task UpdateDataAsync(ProjectConfiguration project, double width, double height)
    {
        _lastProject = project;
        double zoom = project.Zoom;
        double cx = project.CenterX;
        double cy = project.CenterY;
        
        _lastCx = cx; _lastCy = cy; _lastZoom = zoom;

        var (minLon, minLat, maxLon, maxLat) = GetViewportBounds(cx, cy, zoom, width, height);
        var bbox = new BoundingBox(minLon, minLat, maxLon, maxLat);

        foreach (var layer in project.Layers)
        {
            if (!layer.IsVisible) 
            {
                _geometryCache.TryRemove(layer.Id, out _);
                continue;
            }

            var sourceConfig = project.DataSources.Find(s => s.Id == layer.DataSourceId);
            if (sourceConfig == null) continue;

            try
            {
                // Collect required attributes for rules
                List<string>? properties = null;
                if (layer.Rules != null && layer.Rules.Any())
                {
                    properties = layer.Rules
                        .Where(r => r.Filter != null)
                        .Select(r => r.Filter!.ColumnName)
                        .Distinct()
                        .ToList();
                }

                var provider = _providerFactory.Create(layer, sourceConfig);
                if (provider.IsVector)
                {
                    // Fetch data with properties
                    var geometries = await provider.GetGeometriesAsync(bbox, (int)zoom, properties);
                    _geometryCache[layer.Id] = new List<Geometry>(geometries);
                }
            }
            catch (Exception) { /* Log */ }
        }
    }

    public void Draw(SKCanvas canvas, double width, double height)
    {
        canvas.Clear(SKColors.LightGray);

        if (_lastProject == null) return;
        
        double cx = _lastCx; 
        double cy = _lastCy; 
        double zoom = _lastZoom;

        // Draw in Z-order
        foreach (var layer in _lastProject.Layers)
        {
            if (!layer.IsVisible) continue;
            
            if (_geometryCache.TryGetValue(layer.Id, out var geometries))
            {
                DrawGeometries(canvas, geometries, layer, cx, cy, zoom, width, height);
            }
        }
    }

    private void DrawGeometries(SKCanvas canvas, IEnumerable<Geometry> geometries, LayerConfig layer, double cx, double cy, double zoom, double w, double h)
    {
        // Default Paints
        using var defaultFill = CreatePaint(SKPaintStyle.Fill, layer.FillColor, layer.IsFillVisible, layer.Opacity);
        using var defaultStroke = CreatePaint(SKPaintStyle.Stroke, layer.StrokeColor, true, layer.Opacity, layer.StrokeWidth);



        // Simple Paint Cache: Key = "Fill|#RRGGBB|Opacity" or "Stroke|#RRGGBB|Width|Opacity"
        // Since we are inside a loop, let's just manage a Dictionary for this frame
        var paintCache = new Dictionary<string, SKPaint>();

        SKPaint GetCachedPaint(SKPaintStyle style, string color, bool visible, double opacity, double width = 1)
        {
             string key = $"{style}|{color}|{visible}|{opacity}|{width}";
             if (paintCache.TryGetValue(key, out var p)) return p;
             
             p = CreatePaint(style, color, visible, opacity, width);
             paintCache[key] = p;
             return p;
        }

        foreach (var geom in geometries)
        {
            // Determine Style for this Feature
            var rule = GetMatchingRule(geom, layer);
            
            // Use rule style if matched, otherwise default
            SKPaint fillPaint = defaultFill;
            SKPaint strokePaint = defaultStroke;
            
            if (rule != null)
            {
                if (!rule.IsVisible) continue;
                fillPaint = GetCachedPaint(SKPaintStyle.Fill, rule.FillColor, true, layer.Opacity); 
                strokePaint = GetCachedPaint(SKPaintStyle.Stroke, rule.StrokeColor, true, layer.Opacity, rule.StrokeWidth);
            }

            // Draw without disposing paints (they are cached or default)
            DrawGeometry(canvas, geom, fillPaint, strokePaint, cx, cy, zoom, w, h);
        }

        // Dispose cached paints
        foreach(var p in paintCache.Values) p.Dispose();
        paintCache.Clear();
    }

    private void DrawGeometry(SKCanvas canvas, Geometry geom, SKPaint fill, SKPaint stroke, double cx, double cy, double zoom, double w, double h)
    {
        if (geom is Polygon poly)
        {
            var path = GeometryToPath(poly, cx, cy, zoom, w, h);
            if (fill.Color.Alpha > 0) canvas.DrawPath(path, fill);
            canvas.DrawPath(path, stroke);
        }
        else if (geom is MultiPolygon multiPoly)
        {
            foreach (var p in multiPoly.Geometries)
            {
                if (p is Polygon subPoly)
                {
                    var path = GeometryToPath(subPoly, cx, cy, zoom, w, h);
                    if (fill.Color.Alpha > 0) canvas.DrawPath(path, fill);
                    canvas.DrawPath(path, stroke);
                }
            }
        }
        // LineString, Point support...
    }

    private SKPaint CreatePaint(SKPaintStyle style, string colorHex, bool visible, double opacity, double strokeWidth = 1)
    {
        var paint = new SKPaint
        {
            Style = style,
            Color = SKColors.Transparent,
            StrokeWidth = (float)strokeWidth,
            IsAntialias = true
        };

        if (visible && SKColor.TryParse(colorHex, out var color))
        {
            paint.Color = color.WithAlpha((byte)(opacity * 255));
        }
        return paint;
    }

    private StyleRule? GetMatchingRule(Geometry geom, LayerConfig layer)
    {
        if (layer.Rules == null || layer.Rules.Count == 0) return null;

        var attributes = geom.UserData as IDictionary<string, object>;

        foreach (var rule in layer.Rules)
        {
            if (rule.Filter == null) return rule; // Default/Else rule

            // If attributes missing but rule requires filter, skip? Or fail? Skip.
            if (attributes != null && EvaluateFilter(rule.Filter, attributes))
            {
                return rule;
            }
        }
        return null; // Return null to use default layer style
    }

    private bool EvaluateFilter(FilterCondition filter, IDictionary<string, object> attributes)
    {
        if (!attributes.TryGetValue(filter.ColumnName, out var val) || val == null) return false;
        
        string sVal = val.ToString() ?? "";
        string fVal = filter.Value;

        switch (filter.Operator)
        {
            case FilterOperator.Equals: return sVal.Equals(fVal, StringComparison.OrdinalIgnoreCase);  // Case insensitive?
            case FilterOperator.NotEquals: return !sVal.Equals(fVal, StringComparison.OrdinalIgnoreCase);
            case FilterOperator.Contains: return sVal.IndexOf(fVal, StringComparison.OrdinalIgnoreCase) >= 0;
            // TODO: Numeric comparison handling
            default: return false;
        }
    }

    private SKPath GeometryToPath(Geometry geom, double cx, double cy, double zoom, double w, double h)
    {
        var path = new SKPath();
        var coords = geom.Coordinates;
        if (coords.Length == 0) return path;

        var startPt = LatLonToScreen(coords[0].Y, coords[0].X, cx, cy, zoom, w, h);
        path.MoveTo((float)startPt.X, (float)startPt.Y);

        for (int i = 1; i < coords.Length; i++)
        {
            var pt = LatLonToScreen(coords[i].Y, coords[i].X, cx, cy, zoom, w, h);
            path.LineTo((float)pt.X, (float)pt.Y);
        }
        
        if (geom is Polygon) path.Close();
        
        return path;
    }

    private (double minLon, double minLat, double maxLon, double maxLat) GetViewportBounds(double cx, double cy, double zoom, double w, double h)
    {
        // ... (Same as before)
         double n = Math.Pow(2, zoom);
        double degPerPixelX = 360.0 / (256 * n);
        
        double halfW = w / 2.0;
        double halfH = h / 2.0;
        
        double minLon = cx - (halfW * degPerPixelX);
        double maxLon = cx + (halfW * degPerPixelX);
        
        double minLat = cy - (halfH * degPerPixelX); 
        double maxLat = cy + (halfH * degPerPixelX);

        return (minLon, minLat, maxLon, maxLat);
    }
    
    private SKPoint LatLonToScreen(double lat, double lon, double cx, double cy, double zoom, double w, double h)
    {
        // ... (Same as before)
         // Center Pixel
        var centerPx = LatLonToPixel(cy, cx, zoom);
        // Target Pixel
        var targetPx = LatLonToPixel(lat, lon, zoom);
        
        // Screen relative
        double x = targetPx.X - centerPx.X + (w / 2.0);
        double y = targetPx.Y - centerPx.Y + (h / 2.0);
        
        return new SKPoint((float)x, (float)y);
    }
    
    // Reuse LatLonToPixel logic
    private (double X, double Y) LatLonToPixel(double lat, double lon, double zoom)
    {
        lat = Math.Clamp(lat, -85.05112878, 85.05112878);
        double n = Math.Pow(2, zoom);
        double x = (lon + 180.0) / 360.0 * 256.0 * n;
        double latRad = lat * Math.PI / 180.0;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * 256.0 * n;
        return (x, y);
    }
}
