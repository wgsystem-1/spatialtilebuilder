namespace SpatialTileBuilder.Infrastructure.Data;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;

public class PostGISDataProvider : ILayerDataProvider
{
    private readonly IDataSourceService _dataSourceService;
    private readonly string _schema;
    private readonly string _table;
    private readonly string? _labelColumn;

    public string DataSourceId => _dataSourceService.Id;
    public bool IsVector => true;

    public PostGISDataProvider(IDataSourceService dataSourceService, string schema, string table, string? labelColumn = null)
    {
        _dataSourceService = dataSourceService;
        _schema = schema;
        _table = table;
        _labelColumn = labelColumn;
    }

    public async Task<IEnumerable<Geometry>> GetGeometriesAsync(BoundingBox bbox, int zoom, IEnumerable<string>? properties = null)
    {
        // Calculate pixel size for simplification (optional optimization)
        double resolution = (bbox.MaxX - bbox.MinX) / 256.0; // Rough estimate per tile
        
        return await _dataSourceService.GetGeometriesAsync(_schema, _table, bbox, properties, resolution);
    }

    public Task<byte[]> GetImageBytesAsync(BoundingBox bbox, int width, int height)
    {
        return Task.FromResult(Array.Empty<byte>());
    }
}
