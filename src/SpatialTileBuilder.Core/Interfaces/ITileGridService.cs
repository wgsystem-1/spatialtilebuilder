namespace SpatialTileBuilder.Core.Interfaces;

using System.Collections.Generic;
using NetTopologySuite.Geometries;
using SpatialTileBuilder.Core.DTOs;

public interface ITileGridService
{
    IEnumerable<TileIndex> GetTilesInBbox(BoundingBox bbox, int zoom);
    IEnumerable<TileIndex> GetTilesInPolygon(Geometry polygon, int zoom);
    long CalculateTotalTiles(BoundingBox bbox, int minZoom, int maxZoom);
    BoundingBox GetTileBbox(int z, int x, int y);
    TileIndex LatLonToTile(double lat, double lon, int zoom);
}
