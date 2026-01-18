namespace SpatialTileBuilder.Core.DTOs;

public enum DataSourceType
{
    PostGIS,
    Shapefile,
    GeoJson,
    Raster,
    MBTiles
}

public record DataSourceConfig(
    string Id,           // Unique GUID
    string Name,         // Display Name (e.g. "Seoul DB", "Satellite Image")
    DataSourceType Type,
    string ConnectionString, // PostGIS connection string or File Path
    string Provider      // "Npgsql", "OGR", "GDAL", etc. (Optional for now)
);
