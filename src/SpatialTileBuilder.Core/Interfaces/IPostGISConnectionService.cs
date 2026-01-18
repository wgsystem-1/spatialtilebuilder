namespace SpatialTileBuilder.Core.Interfaces;

using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;

public interface IPostGISConnectionService
{
    void SetConnectionInfo(ConnectionInfo info);
    ConnectionInfo? GetCurrentConnectionInfo();
    Task<bool> TestConnectionAsync(ConnectionInfo info);
    Task<List<SpatialTable>> GetSpatialTablesAsync();
    Task<BoundingBox> GetLayerExtentAsync(string schema, string table);
    Task<GeometryType> GetGeometryTypeAsync(string schema, string table);
    Task<List<NetTopologySuite.Geometries.Geometry>> GetGeometriesAsync(string schema, string table, BoundingBox bbox3857, IEnumerable<string>? properties = null, double pixelSize = 0);
    Task<List<string>> GetColumnsAsync(string schema, string table);
    IDbConnection CreateConnection();
}
