namespace SpatialTileBuilder.Infrastructure.Services;

using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;

public class TileGridService : ITileGridService
{
    private const int TileSize = 256;
    private const double MaxLat = 85.05112878;
    private const double MinLat = -85.05112878;
    private const double EarthRadius = 6378137;

    public IEnumerable<TileIndex> GetTilesInBbox(BoundingBox bbox, int zoom)
    {
        var minTile = LatLonToTile(bbox.MinY, bbox.MinX, zoom);
        var maxTile = LatLonToTile(bbox.MaxY, bbox.MaxX, zoom);

        // XYZ scheme: X increases East, Y increases South
        // So MinY(Lat) -> MaxY(TileY), MaxY(Lat) -> MinY(TileY)
        int xMin = Math.Min(minTile.X, maxTile.X);
        int xMax = Math.Max(minTile.X, maxTile.X);
        int yMin = Math.Min(minTile.Y, maxTile.Y);
        int yMax = Math.Max(minTile.Y, maxTile.Y);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                yield return new TileIndex(zoom, x, y);
            }
        }
    }

    public IEnumerable<TileIndex> GetTilesInPolygon(Geometry polygon, int zoom)
    {
        // 1. Calculate BBOX of polygon
        var envelope = polygon.EnvelopeInternal;
        var bbox = new BoundingBox(envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY); // Assume EPSG:4326 input for now

        // 2. Iterate tiles in BBOX
        foreach (var tile in GetTilesInBbox(bbox, zoom))
        {
            // 3. Check intersection for each tile
            var tileBbox = GetTileBbox(tile.Z, tile.X, tile.Y);
            var tilePoly = CreatePolygonFromBbox(tileBbox);

            if (polygon.Intersects(tilePoly))
            {
                yield return tile;
            }
        }
    }

    public long CalculateTotalTiles(BoundingBox bbox, int minZoom, int maxZoom)
    {
        long total = 0;
        for (int z = minZoom; z <= maxZoom; z++)
        {
            var minTile = LatLonToTile(bbox.MinY, bbox.MinX, z);
            var maxTile = LatLonToTile(bbox.MaxY, bbox.MaxX, z);

            long width = Math.Abs(maxTile.X - minTile.X) + 1;
            long height = Math.Abs(maxTile.Y - minTile.Y) + 1;
            total += width * height;
        }
        return total;
    }

    public BoundingBox GetTileBbox(int z, int x, int y)
    {
        double n = Math.Pow(2, z);
        double lonDegPerTile = 360.0 / n;
        
        double minLon = (x * lonDegPerTile) - 180.0;
        double maxLon = ((x + 1) * lonDegPerTile) - 180.0;
        
        double maxLat = TileYToLat(y, z);
        double minLat = TileYToLat(y + 1, z);

        return new BoundingBox(minLon, minLat, maxLon, maxLat);
    }

    public TileIndex LatLonToTile(double lat, double lon, int zoom)
    {
        lat = Math.Clamp(lat, MinLat, MaxLat);
        
        int x = (int)((lon + 180.0) / 360.0 * (1 << zoom));
        int y = (int)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 
            1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

        return new TileIndex(zoom, x, y);
    }

    // Helper methods
    private double TileYToLat(int y, int z)
    {
        double n = Math.PI - 2.0 * Math.PI * y / Math.Pow(2, z);
        return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    }

    private Geometry CreatePolygonFromBbox(BoundingBox bbox)
    {
         var factory = new GeometryFactory();
         var coords = new Coordinate[]
         {
             new Coordinate(bbox.MinX, bbox.MinY),
             new Coordinate(bbox.MaxX, bbox.MinY),
             new Coordinate(bbox.MaxX, bbox.MaxY),
             new Coordinate(bbox.MinX, bbox.MaxY),
             new Coordinate(bbox.MinX, bbox.MinY)
         };
         return factory.CreatePolygon(coords);
    }
}
