namespace SpatialTileBuilder.Core.Interfaces;

using System.Collections.Generic;
using System.Threading.Tasks;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;
using NetTopologySuite.Geometries;

public interface IDataSourceService
{
    string Id { get; }
    string Name { get; }
    DataSourceType Type { get; }
    
    // Connect/Disconnect
    Task<bool> TestConnectionAsync();
    
    // Schema Discovery
    Task<List<SpatialTable>> GetTablesAsync();
    Task<List<string>> GetColumnsAsync(string schema, string table);
    Task<BoundingBox> GetLayerExtentAsync(string schema, string table);
    Task<GeometryType> GetGeometryTypeAsync(string schema, string table);
    Task<List<string>> GetUniqueValuesAsync(string schema, string table, string column);

    // Data Fetching
    Task<IEnumerable<Geometry>> GetGeometriesAsync(string schema, string table, BoundingBox bbox, IEnumerable<string>? properties = null, double pixelSize = 0);
}
