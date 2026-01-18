namespace SpatialTileBuilder.Infrastructure.Data;

using System;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using SpatialTileBuilder.Infrastructure.Services;

public class DataSourceFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public DataSourceFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IDataSourceService Create(DataSourceConfig config)
    {
        return config.Type switch
        {
            DataSourceType.PostGIS => new PostGISDataSource(config, _loggerFactory.CreateLogger<PostGISDataSource>()),
            // Future file-based
            _ => throw new NotSupportedException($"DataSource Type {config.Type} not supported yet.")
        };
    }
}
