namespace SpatialTileBuilder.Infrastructure.Data;

using System;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;
using SpatialTileBuilder.Core.Interfaces;

public class LayerDataProviderFactory
{
    private readonly DataSourceFactory _dataSourceFactory;

    public LayerDataProviderFactory(DataSourceFactory dataSourceFactory)
    {
        _dataSourceFactory = dataSourceFactory;
    }

    public ILayerDataProvider Create(LayerConfig layer, DataSourceConfig sourceConfig)
    {
        // 1. Get basic data source service
        var dsService = _dataSourceFactory.Create(sourceConfig);

        // 2. Create specific provider based on type
        switch (sourceConfig.Type)
        {
            case DataSourceType.PostGIS:
                // Assuming layer.SourceName implies Table Name for PostGIS
                // Schema parsing might be needed if SourceName is "schema.table"
                var parts = layer.SourceName.Split('.');
                string schema = parts.Length > 1 ? parts[0] : "public";
                string table = parts.Length > 1 ? parts[1] : layer.SourceName;
                
                return new PostGISDataProvider(dsService, schema, table, layer.LabelColumn);

            case DataSourceType.Shapefile:
            case DataSourceType.GeoJson:
                 // TODO: Implement ShapefileDataProvider
                 // For now, reuse PostGISDataProvider logic if possible or throw
                 throw new NotImplementedException("Shapefile provider not yet implemented for rendering.");

            case DataSourceType.Raster:
                 throw new NotImplementedException("Raster provider not yet implemented.");

            default:
                throw new NotSupportedException($"DataSource Type {sourceConfig.Type} not supported for rendering.");
        }
    }
}
