namespace SpatialTileBuilder.Infrastructure.Mapnik;

using SkiaSharp;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;

public class MockMapnikRenderer : IMapnikRenderer
{
    private readonly IPostGISConnectionService _connectionService;
    private List<LayerConfig> _layers = new();


    public MockMapnikRenderer(IPostGISConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public void Dispose() { }

    public void LoadStyle(string xmlPath) { }

    public void SetDatasource(string connectionString) { }

    public void SetLayers(List<LayerConfig> layers)
    {
        _layers = layers;
    }

    public byte[]? RenderTile(int z, int x, int y, int tileSize = 256)
    {
        var bbox = TileToBBox(x, y, z);

        using var surface = SKSurface.Create(new SKImageInfo(tileSize, tileSize));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        
        // Thread-Local label bounds for collision detection within this tile
        var labelBounds = new List<SKRect>();

        bool drawnSomething = false;

        try 
        {
            if (_layers != null)
            {
                foreach (var layer in _layers)
                {
                   if (!layer.IsVisible) continue;

                   // Calculate pixelSize (resolution in meters/pixel)
                   double resolution = (bbox.MaxX - bbox.MinX) / tileSize;

                   // Parse schema/table
                   string schema = "public";
                   string table = layer.SourceName;
                   if (table.Contains("."))
                   {
                       var parts = table.Split('.');
                       schema = parts[0];
                       table = parts[1];
                   }

                   // Collect properties
                   List<string>? properties = null;
                   if (layer.Rules != null && layer.Rules.Any())
                   {
                       properties = layer.Rules
                           .Where(r => r.Filter != null)
                           .Select(r => r.Filter!.ColumnName)
                           .Distinct()
                           .ToList();
                   }
                   if (!string.IsNullOrEmpty(layer.LabelColumn))
                   {
                       if (properties == null) properties = new List<string>();
                       if (!properties.Contains(layer.LabelColumn)) properties.Add(layer.LabelColumn);
                   }

                   var list = _connectionService.GetGeometriesAsync(schema, table, bbox, properties, resolution).Result;
                   if (list != null && list.Count > 0)
                   {
                        drawnSomething = true;
                        foreach (var geom in list)
                        {
                            DrawGeometry(canvas, geom, bbox, tileSize, tileSize, layer, labelBounds);
                        }
                   }
                }
            }
        }
        catch (Exception) { }

        if (!drawnSomething) return null;

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private void DrawGeometry(SKCanvas canvas, Geometry geom, BoundingBox bbox, int width, int height, LayerConfig layer, List<SKRect> labelBounds)
    {
        double rangeX = bbox.MaxX - bbox.MinX;
        double rangeY = bbox.MaxY - bbox.MinY;

        SKPoint Transform(Coordinate c)
        {
             float px = (float)((c.X - bbox.MinX) / rangeX * width);
             float py = (float)((bbox.MaxY - c.Y) / rangeY * height); 
             return new SKPoint(px, py);
        }

        // Determine Style
        var rule = GetMatchingRule(geom, layer);
        if (rule != null && !rule.IsVisible) return; // Hidden by rule

        // Fallback or Rule Style
        string fillColor = rule?.FillColor ?? layer.FillColor;
        bool isFillVisible = rule?.IsVisible ?? layer.IsFillVisible; // Rule IsVisible usually implies "Show this rule", but assumed true if matched. Using Layer's general toggle?
        // Actually StyleRule IsVisible is explicit. If rule matched and IsVisible=false, we skip.
        // Assuming if rule matched, we use rule's colors.
        
        string strokeColor = rule?.StrokeColor ?? layer.StrokeColor;
        double strokeWidth = rule?.StrokeWidth ?? layer.StrokeWidth;
        double opacity = layer.Opacity; // Apply layer opacity globaly

        // Paints
        using var fillPaint = new SKPaint
        {
            Color = ParseColor(fillColor, opacity),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Color = ParseColor(strokeColor, opacity), // Stroke opacity separate? Or assume 1? Let's use 1 for now or blend opacity.
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)strokeWidth,
            IsAntialias = true
        };
        
        // Dash Support in LayerConfig?
        if (layer.StrokeDashArray == "Dash") strokePaint.PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0);
        else if (layer.StrokeDashArray == "Dot") strokePaint.PathEffect = SKPathEffect.CreateDash(new float[] { 2, 2 }, 0);

        using var pointPaint = new SKPaint
        {
            Color = ParseColor(layer.PointColor, opacity), // Point color not in Rule yet? StyleRule has colors.
            // StyleRule defines FillColor/StrokeColor. Let's use FillColor for Point.
            // Wait, LayerConfig has PointColor. Rule doesn't have PointColor explicitly, uses FillColor?
            // Let's use FillColor from rule for Point if rule exists.
            
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        if (rule != null) pointPaint.Color = ParseColor(rule.FillColor, opacity);

        if (geom is Point p)
        {
            var pt = Transform(p.Coordinate);
            canvas.DrawCircle(pt, (float)layer.PointSize, pointPaint);
            DrawLabel(canvas, layer, geom, pt, labelBounds);
        }
        else if (geom is LineString ls)
        {
            using var path = new SKPath();
            var coords = ls.Coordinates;
            if (coords.Length > 1)
            {
                path.MoveTo(Transform(coords[0]));
                for(int i = 1; i < coords.Length; i++) path.LineTo(Transform(coords[i]));
                canvas.DrawPath(path, strokePaint);
                
                // Label at midpoint
                if (!string.IsNullOrEmpty(layer.LabelColumn))
                {
                    var midIndex = coords.Length / 2;
                    var pt = Transform(coords[midIndex]);
                    DrawLabel(canvas, layer, geom, pt, labelBounds);
                }
            }
        }
        else if (geom is Polygon poly)
        {
            using var path = new SKPath();
            var exCoords = poly.ExteriorRing.Coordinates;
            if (exCoords.Length > 0)
            {
                path.MoveTo(Transform(exCoords[0]));
                for (int i = 1; i < exCoords.Length; i++) path.LineTo(Transform(exCoords[i]));
                path.Close();
                
                foreach(var hole in poly.InteriorRings)
                {
                    var holeCoords = hole.Coordinates;
                     if (holeCoords.Length > 0)
                    {
                        path.MoveTo(Transform(holeCoords[0]));
                        for (int i = 1; i < holeCoords.Length; i++) path.LineTo(Transform(holeCoords[i]));
                        path.Close();
                    }
                }

                path.FillType = SKPathFillType.EvenOdd;
                
                if (isFillVisible) canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, strokePaint);
                
                // Label at centroid
                if (!string.IsNullOrEmpty(layer.LabelColumn))
                {
                    var centroid = poly.Centroid;
                    if (centroid != null)
                    {
                        DrawLabel(canvas, layer, geom, Transform(centroid.Coordinate), labelBounds);
                    }
                }
            }
        }
        else if (geom is MultiLineString mls)
        {
            foreach (var g in mls.Geometries) DrawGeometry(canvas, g, bbox, width, height, layer, labelBounds);
        }
        else if (geom is MultiPolygon mpoly)
        {
            foreach (var g in mpoly.Geometries) DrawGeometry(canvas, g, bbox, width, height, layer, labelBounds);
        }
        else if (geom is GeometryCollection gc)
        {
            foreach (var g in gc.Geometries) DrawGeometry(canvas, g, bbox, width, height, layer, labelBounds);
        }
    }
    
    private StyleRule? GetMatchingRule(Geometry geom, LayerConfig layer)
    {
        if (layer.Rules == null || layer.Rules.Count == 0) return null;

        var attributes = geom.UserData as IDictionary<string, object>;

        foreach (var rule in layer.Rules)
        {
            if (rule.Filter == null) return rule; 
            if (attributes != null && EvaluateFilter(rule.Filter, attributes))
            {
                return rule;
            }
        }
        return null; 
    }

    private bool EvaluateFilter(FilterCondition filter, IDictionary<string, object> attributes)
    {
        if (!attributes.TryGetValue(filter.ColumnName, out var val) || val == null) return false;
        
        string sVal = val.ToString() ?? "";
        string fVal = filter.Value;

        switch (filter.Operator)
        {
            case FilterOperator.Equals: return sVal.Equals(fVal, StringComparison.OrdinalIgnoreCase); 
            case FilterOperator.NotEquals: return !sVal.Equals(fVal, StringComparison.OrdinalIgnoreCase);
            case FilterOperator.Contains: return sVal.IndexOf(fVal, StringComparison.OrdinalIgnoreCase) >= 0;
            default: return false;
        }
    }

    private void DrawLabel(SKCanvas canvas, LayerConfig layer, Geometry geom, SKPoint position, List<SKRect> labelBounds)
    {
        if (string.IsNullOrEmpty(layer.LabelColumn)) return;

        // UserData handling
        string text = "";
        if (geom.UserData is IDictionary<string, object> attrs)
        {
            if (attrs.TryGetValue(layer.LabelColumn, out var val) && val != null)
                text = val.ToString() ?? "";
        }
        else if (geom.UserData is IDictionary<string, string> strAttrs)
        {
             if (strAttrs.TryGetValue(layer.LabelColumn, out var val) && val != null)
                text = val;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        // Create Typeface
        using var typeface = (!string.IsNullOrEmpty(layer.FontName)) 
            ? SKTypeface.FromFamilyName(layer.FontName)
            : SKTypeface.FromFamilyName("Arial");

        using var paint = new SKPaint
        {
            Color = ParseColor(layer.LabelColor, 1.0),
            TextSize = (float)layer.LabelSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = typeface
        };

        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        
        var halfW = bounds.Width / 2;
        var halfH = bounds.Height / 2;
        var rect = new SKRect(position.X - halfW, position.Y - halfH, position.X + halfW, position.Y + halfH);
        
        rect.Inflate(2, 2);

        foreach(var occupied in labelBounds)
        {
            if (rect.IntersectsWith(occupied)) return; 
        }

        if (layer.LabelHaloRadius > 0)
        {
            using var haloPaint = new SKPaint
            {
                Color = SKColors.White, // Default halo white
                TextSize = (float)layer.LabelSize,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)layer.LabelHaloRadius * 2,
                Typeface = typeface
            };
             canvas.DrawText(text, position.X, position.Y - bounds.MidY, haloPaint);
        }

        canvas.DrawText(text, position.X, position.Y - bounds.MidY, paint);
        labelBounds.Add(rect); // Use local list
    }

    private SKColor ParseColor(string hex, double opacity)
    {
        if (string.IsNullOrEmpty(hex)) return SKColors.Black;
        if (SKColor.TryParse(hex, out var color))
        {
            return color.WithAlpha((byte)(opacity * 255));
        }
        return SKColors.Gray;
    }

    private BoundingBox TileToBBox(int x, int y, int z)
    {
        // EPSG:3857 Bounds
        double max = 20037508.34;
        double res = (max * 2) / Math.Pow(2, z);
        
        double tileMinX = -max + (x * res);
        double tileMaxX = -max + ((x + 1) * res);
        
        double tileMaxY = max - (y * res);
        double tileMinY = max - ((y + 1) * res);
        
        return new BoundingBox(tileMinX, tileMinY, tileMaxX, tileMaxY);
    }
}
