namespace SpatialTileBuilder.Infrastructure.Mapnik;

using SkiaSharp;
using SpatialTileBuilder.Core.Interfaces;
using System;
using System.IO;

public class MockMapnikRenderer : IMapnikRenderer
{
    private readonly IPostGISConnectionService _connectionService;
    private System.Collections.Generic.List<SpatialTileBuilder.Core.DTOs.LayerStyle> _layers = new();

    public MockMapnikRenderer(IPostGISConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public void Dispose() { }

    public void LoadStyle(string xmlPath) { }

    public void SetDatasource(string connectionString) { }

    public void SetLayers(System.Collections.Generic.List<SpatialTileBuilder.Core.DTOs.LayerStyle> layers)
    {
        _layers = layers;
    }

    public byte[] RenderTile(int z, int x, int y, int tileSize = 256)
    {
        var bbox = TileToBBox(x, y, z);

        using var surface = SKSurface.Create(new SKImageInfo(tileSize, tileSize));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent); // Make background transparent or white? Usually transparent for tiles.

        // Draw a light border for debugging? Or remove it for final. 
        // Let's keep a very faint one or remove it if user wants clean tiles.
        // using var borderPaint = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        // canvas.DrawRect(0, 0, tileSize, tileSize, borderPaint);

        try 
        {
            if (_layers != null)
            {
                foreach (var layer in _layers)
                {
                   if (!layer.IsVisible) continue; // Skip invisible

                   var list = _connectionService.GetGeometriesAsync(layer.TableInfo.Schema, layer.TableInfo.Table, bbox).Result;
                   if (list != null)
                   {
                        foreach (var geom in list)
                        {
                            DrawGeometry(canvas, geom, bbox, tileSize, tileSize, layer);
                        }
                   }
                }
            }
        }
        catch (Exception) { }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private void DrawGeometry(SKCanvas canvas, NetTopologySuite.Geometries.Geometry geom, SpatialTileBuilder.Core.DTOs.BoundingBox bbox, int width, int height, SpatialTileBuilder.Core.DTOs.LayerStyle style)
    {
        double rangeX = bbox.MaxX - bbox.MinX;
        double rangeY = bbox.MaxY - bbox.MinY;

        SKPoint Transform(NetTopologySuite.Geometries.Coordinate c)
        {
             float px = (float)((c.X - bbox.MinX) / rangeX * width);
             float py = (float)((bbox.MaxY - c.Y) / rangeY * height); 
             return new SKPoint(px, py);
        }

        // Parse Paints
        using var fillPaint = new SKPaint
        {
            Color = ParseColor(style.FillColor, style.Opacity),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Color = ParseColor(style.StrokeColor, 1.0), // Stroke opacity separate? Or assume 1? Let's use 1 for now or blend opacity.
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)style.StrokeWidth,
            IsAntialias = true
        };
        
        if (style.StrokeDashArray == "Dash") strokePaint.PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0);
        else if (style.StrokeDashArray == "Dot") strokePaint.PathEffect = SKPathEffect.CreateDash(new float[] { 2, 2 }, 0);

        using var pointPaint = new SKPaint
        {
            Color = ParseColor(style.PointColor, 1.0),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        if (geom is NetTopologySuite.Geometries.Point p)
        {
            var pt = Transform(p.Coordinate);
            canvas.DrawCircle(pt, (float)style.PointSize, pointPaint);
        }
        else if (geom is NetTopologySuite.Geometries.LineString ls)
        {
            using var path = new SKPath();
            var coords = ls.Coordinates;
            if (coords.Length > 1)
            {
                path.MoveTo(Transform(coords[0]));
                for(int i = 1; i < coords.Length; i++) path.LineTo(Transform(coords[i]));
                canvas.DrawPath(path, strokePaint);
            }
        }
        else if (geom is NetTopologySuite.Geometries.Polygon poly)
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
                
                if (style.IsFillVisible) canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, strokePaint);
            }
        }
        else if (geom is NetTopologySuite.Geometries.MultiLineString mls)
        {
            foreach (var g in mls.Geometries) DrawGeometry(canvas, g, bbox, width, height, style);
        }
        else if (geom is NetTopologySuite.Geometries.MultiPolygon mpoly)
        {
            foreach (var g in mpoly.Geometries) DrawGeometry(canvas, g, bbox, width, height, style);
        }
        else if (geom is NetTopologySuite.Geometries.GeometryCollection gc)
        {
            foreach (var g in gc.Geometries) DrawGeometry(canvas, g, bbox, width, height, style);
        }
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

    private SpatialTileBuilder.Core.DTOs.BoundingBox TileToBBox(int x, int y, int z)
    {
        // EPSG:3857 Bounds
        double max = 20037508.34;
        // Tile Size in meters
        double res = (max * 2) / Math.Pow(2, z);
        
        double tileMinX = -max + (x * res);
        double tileMaxX = -max + ((x + 1) * res);
        
        // Y origin for TMS is bottom-left, but for XYZ (Google/OSM) it is top-left.
        // We usually use XYZ.
        // For XYZ: 0 is top.
        double tileMaxY = max - (y * res);
        double tileMinY = max - ((y + 1) * res);
        
        return new SpatialTileBuilder.Core.DTOs.BoundingBox(tileMinX, tileMinY, tileMaxX, tileMaxY);
    }
}

