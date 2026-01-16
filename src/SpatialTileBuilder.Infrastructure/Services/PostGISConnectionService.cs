namespace SpatialTileBuilder.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;
using SpatialTileBuilder.Core.Interfaces;

public sealed class PostGISConnectionService : IPostGISConnectionService, IDisposable
{
    private readonly ILogger<PostGISConnectionService> _logger;
    private NpgsqlDataSource? _dataSource;
    private ConnectionInfo? _currentInfo;

    public PostGISConnectionService(ILogger<PostGISConnectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void SetConnectionInfo(ConnectionInfo info)
    {
        if (_currentInfo == info && _dataSource != null) return;

        _dataSource?.Dispose();
        _currentInfo = info;

        var builder = new NpgsqlDataSourceBuilder(BuildConnectionString(info));
        builder.UseNetTopologySuite();
        _dataSource = builder.Build();
    }

    public ConnectionInfo? GetCurrentConnectionInfo() => _currentInfo;

    public async Task<bool> TestConnectionAsync(ConnectionInfo info)
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(BuildConnectionString(info));
            using var ds = builder.Build();
            using var conn = ds.CreateConnection();
            await conn.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed.");
            return false;
        }
    }

    public IDbConnection CreateConnection()
    {
        if (_dataSource == null)
            throw new InvalidOperationException("Connection info not set.");
        
        return _dataSource.CreateConnection();
    }

    public async Task<List<SpatialTable>> GetSpatialTablesAsync()
    {
        using var conn = CreateConnection();
        
        var sql = @"
            SELECT 
                f_table_schema as Schema, 
                f_table_name as Table, 
                f_geometry_column as GeometryColumn, 
                type as GeometryType, 
                srid as Srid
            FROM geometry_columns
            ORDER BY f_table_schema, f_table_name";
        
        try
        {
            var result = await conn.QueryAsync<SpatialTable>(sql);
            var list = result.AsList();

            if (list.Count > 0) return list;

            // Fallback: If geometry_columns is empty (permissions or unregistered tables),
            // search ALL user schemas for columns with geometry-like types.
            // This is crucial for users with custom schemas like 'onmap_db' etc.
            var fallbackSql = @"
                SELECT 
                    table_schema as Schema, 
                    table_name as Table, 
                    column_name as GeometryColumn, 
                    udt_name as GeometryType, 
                    0 as Srid
                FROM information_schema.columns
                WHERE table_schema NOT IN ('information_schema', 'pg_catalog') 
                  AND table_schema NOT LIKE 'pg_%'
                  AND (udt_name IN ('geometry', 'geography') OR data_type = 'USER-DEFINED')
                ORDER BY table_schema, table_name";

            var fallbackResult = await conn.QueryAsync<SpatialTable>(fallbackSql);
            
            // Filter results to ensure they look like spatial columns if the type was generic 'USER-DEFINED'
            // We assume if udt_name contains 'geo' it is likely spatial.
            return fallbackResult
                .Where(t => t.GeometryType.Contains("geo", StringComparison.OrdinalIgnoreCase) || t.GeometryType == "USER-DEFINED")
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query geometry_columns.");
            return new List<SpatialTable>();
        }
    }

    public async Task<BoundingBox> GetLayerExtentAsync(string schema, string table)
    {
        using var conn = CreateConnection();
        
        string Quote(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

        var geomCol = await conn.ExecuteScalarAsync<string>(
            "SELECT f_geometry_column FROM geometry_columns WHERE f_table_schema = @Schema AND f_table_name = @Table",
            new { Schema = schema, Table = table });
            
        if (geomCol == null)
        {
             // Fallback search
             var fallbackSql = @"
                SELECT column_name 
                FROM information_schema.columns
                WHERE table_schema = @Schema AND table_name = @Table
                  AND (udt_name IN ('geometry', 'geography') OR data_type = 'USER-DEFINED')";
             geomCol = await conn.ExecuteScalarAsync<string>(fallbackSql, new { Schema = schema, Table = table });
        }

        if (geomCol == null) throw new ArgumentException($"Not a spatial table: {schema}.{table}");

        // Check SRID
        int srid = 0;
        try 
        {
            srid = await conn.ExecuteScalarAsync<int>($"SELECT srid FROM geometry_columns WHERE f_table_schema = '{schema}' AND f_table_name = '{table}'");
        } 
        catch {}
        
        if (srid == 0)
        {
            try { srid = await conn.ExecuteScalarAsync<int>($"SELECT ST_SRID({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1"); } catch {}
        }
        
        if (srid == 0)
        {
             // Heuristic: Check coordinate range to guess if it's Lat/Lon (4326) or Projected (likely 5179/5174)
             try 
             {
                 var xMin = await conn.ExecuteScalarAsync<double?>($"SELECT ST_XMin({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1");
                 if (xMin.HasValue && xMin.Value >= -180.0 && xMin.Value <= 180.0)
                 {
                     srid = 4326;
                     _logger.LogInformation("Guessed SRID 4326 (Lat/Lon) for {Schema}.{Table}", schema, table);
                 }
                 else if (xMin.HasValue && Math.Abs(xMin.Value) > 10_000_000 && Math.Abs(xMin.Value) < 20_037_508)
                 {
                     srid = 3857; // Likely Web Mercator
                     _logger.LogInformation("Guessed SRID 3857 (Web Mercator) for {Schema}.{Table}", schema, table);
                 }
                 else
                 {
                     srid = 5179; // Default Korean projection
                     _logger.LogInformation("Guessed SRID 5179 (Projected) for {Schema}.{Table}", schema, table);
                 }
             }
             catch
             {
                 srid = 5179;
             }
        }

        string geomExpr = $"ST_SetSRID({Quote(geomCol)}, {srid})"; // Force usage of detected SRID

        // Transform to WGS84 (4326) for tile coordinate calculation
        var sql = $"SELECT ST_XMin(ext) as MinX, ST_YMin(ext) as MinY, ST_XMax(ext) as MaxX, ST_YMax(ext) as MaxY FROM (SELECT ST_Extent(ST_Transform({geomExpr}, 4326)) as ext FROM {Quote(schema)}.{Quote(table)}) as t";
        
        var result = await conn.QueryFirstOrDefaultAsync(sql);
        if (result != null && result.MinX != null)
        {
            return new BoundingBox(
                (double)result.MinX,
                (double)result.MinY,
                (double)result.MaxX,
                (double)result.MaxY
            );
        }
        
        return new BoundingBox(0,0,0,0);
    }

    public async Task<GeometryType> GetGeometryTypeAsync(string schema, string table)
    {
         using var conn = CreateConnection();
         var typeStr = await conn.ExecuteScalarAsync<string>(
            "SELECT type FROM geometry_columns WHERE f_table_schema = @Schema AND f_table_name = @Table",
            new { Schema = schema, Table = table });
            
         return ParseGeometryType(typeStr);
    }

    public async Task<List<NetTopologySuite.Geometries.Geometry>> GetGeometriesAsync(string schema, string table, BoundingBox bbox)
    {
        using var conn = CreateConnection();
        string Quote(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

        // Find geometry column and SRID from metadata
        var spatialInfo = await conn.QueryFirstOrDefaultAsync<(string? GeometryColumn, int Srid)>(
            "SELECT f_geometry_column, srid FROM geometry_columns WHERE f_table_schema = @Schema AND f_table_name = @Table",
            new { Schema = schema, Table = table });
            
        string? geomCol = spatialInfo.GeometryColumn;
        int srid = spatialInfo.Srid;

        if (string.IsNullOrEmpty(geomCol))
        {
            // Fallback: try to guess column if not registered in geometry_columns
             var fallbackSql = @"
                SELECT column_name 
                FROM information_schema.columns
                WHERE table_schema = @Schema AND table_name = @Table
                  AND (udt_name IN ('geometry', 'geography') OR data_type = 'USER-DEFINED')";
             geomCol = await conn.ExecuteScalarAsync<string>(fallbackSql, new { Schema = schema, Table = table });
        }

        if (string.IsNullOrEmpty(geomCol)) return new List<NetTopologySuite.Geometries.Geometry>();

        // If SRID is 0, check the actual data
        if (srid == 0)
        {
            try 
            {
                srid = await conn.ExecuteScalarAsync<int>($"SELECT ST_SRID({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1");
            }
            catch { srid = 0; }
        }

        if (srid == 0)
        {
             // Heuristic: Check coordinate range to guess if it's Lat/Lon (4326) or Projected (likely 5179/5174)
             try 
             {
                 var xMin = await conn.ExecuteScalarAsync<double?>($"SELECT ST_XMin({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1");
                 if (xMin.HasValue && xMin.Value >= -180.0 && xMin.Value <= 180.0)
                 {
                     srid = 4326;
                     _logger.LogInformation("Guessed SRID 4326 (Lat/Lon) for {Schema}.{Table}", schema, table);
                 }
                 else if (xMin.HasValue && Math.Abs(xMin.Value) > 10_000_000 && Math.Abs(xMin.Value) < 20_037_508)
                 {
                     srid = 3857; // Likely Web Mercator
                     _logger.LogInformation("Guessed SRID 3857 (Web Mercator) for {Schema}.{Table}", schema, table);
                 }
                 else
                 {
                     srid = 5179; // Default Korean projection
                     _logger.LogInformation("Guessed SRID 5179 (Projected) for {Schema}.{Table}", schema, table);
                 }
             }
             catch
             {
                 srid = 5179;
             }
        }

        string geomExpr = Quote(geomCol);
        // If the ACTUAL data srid is 0 (we just set variable srid=5179 but that's for logic), 
        // we must use ST_SetSRID in SQL.
        // To be safe, let's check what the DB thinks.
        
        // Simpler approach: If we decided srid=5179 but the DB says 0, we need SetSRID.
        // We can just construct the query carefully.
        
        string sql;
        if (srid == 3857 || srid == 900913)
        {
             sql = @$"
                SELECT {Quote(geomCol)} as geom
                FROM {Quote(schema)}.{Quote(table)}
                WHERE ST_Intersects(
                    {Quote(geomCol)}, 
                    ST_MakeEnvelope(@MinX, @MinY, @MaxX, @MaxY, 3857)
                )
                LIMIT 2000";
        }
        else
        {
             // If we suspect the data is raw (srid 0) but we treat it as 'srid' (e.g. 5179),
             // we should wrap it.
             // However, doing ST_SetSRID on data that IS ALREADY 5179 is harmless/redundant but okay.
             // Doing it on data that is 0 is NECESSARY.
             
             // Construct source geometry expression
             string sourceGeom = $"ST_SetSRID({Quote(geomCol)}, {srid})"; 
             // Logic: If data has an SRID, SetSRID just overrides it (essentially force). 
             // This is good if metadata is wrong.
             
             sql = @$"
                SELECT ST_Transform({sourceGeom}, 3857) as geom
                FROM {Quote(schema)}.{Quote(table)}
                WHERE ST_Intersects(
                    ST_Transform({sourceGeom}, 3857), 
                    ST_MakeEnvelope(@MinX, @MinY, @MaxX, @MaxY, 3857)
                )
                LIMIT 2000";
        }

        // Use dynamic query instead of Geometry type to avoid Dapper mapping issues with abstract classes
        var result = await conn.QueryAsync<dynamic>(sql, 
            new { bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY });
        
        var geometryList = new List<NetTopologySuite.Geometries.Geometry>();
        foreach (var row in result)
        {
            if (row.geom is NetTopologySuite.Geometries.Geometry g)
            {
                geometryList.Add(g);
            }
        }

        return geometryList;
    }

    public async Task<List<string>> GetColumnsAsync(string schema, string table)
    {
        using var conn = CreateConnection();
        var sql = @"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @Schema 
              AND table_name = @Table
              AND udt_name NOT IN ('geometry', 'geography') 
              AND data_type NOT IN ('USER-DEFINED')
            ORDER BY ordinal_position";
            
        var columns = await conn.QueryAsync<string>(sql, new { Schema = schema, Table = table });
        return columns.AsList();
    }

    private string BuildConnectionString(ConnectionInfo info)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = info.Host,
            Port = info.Port,
            Database = info.Database,
            Username = info.Username,
            Password = info.Password,
            SslMode = ParseSslMode(info.SslMode),
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            CommandTimeout = 300
        };
        return builder.ConnectionString;
    }

    private SslMode ParseSslMode(string mode)
    {
        return mode?.ToLower() switch
        {
            "disable" => SslMode.Disable,
            "require" => SslMode.Require,
            _ => SslMode.Prefer
        };
    }

    private GeometryType ParseGeometryType(string? type)
    {
        if (type == null) return GeometryType.Unknown;
        
        if (Enum.TryParse<GeometryType>(type, true, out var result))
            return result;
        
        // Handle postgis modifiers like 'POINTM'
        if (type.StartsWith("POINT")) return GeometryType.Point;
        if (type.StartsWith("LINESTRING")) return GeometryType.LineString;
        if (type.StartsWith("POLYGON")) return GeometryType.Polygon;
        if (type.StartsWith("MULTIPOINT")) return GeometryType.MultiPoint;
        if (type.StartsWith("MULTILINESTRING")) return GeometryType.MultiLineString;
        if (type.StartsWith("MULTIPOLYGON")) return GeometryType.MultiPolygon;
            
        return GeometryType.Unknown;
    }

    public void Dispose()
    {
        _dataSource?.Dispose();
    }
}
