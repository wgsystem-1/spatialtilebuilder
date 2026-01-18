namespace SpatialTileBuilder.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;
using SpatialTileBuilder.Core.Interfaces;
using NetTopologySuite.Geometries;

public class PostGISDataSource : IDataSourceService, IDisposable
{
    private readonly DataSourceConfig _config;
    private readonly ILogger _logger;
    private NpgsqlDataSource? _dataSource;

    public string Id => _config.Id;
    public string Name => _config.Name;
    public DataSourceType Type => _config.Type;

    public PostGISDataSource(DataSourceConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(_config.ConnectionString);
            builder.UseNetTopologySuite();
            _dataSource = builder.Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PostGIS DataSource {Name}", Name);
            // Don't throw here, allow TestConnection to fail gracefully
        }
    }

    private IDbConnection CreateConnection()
    {
        if (_dataSource == null) throw new InvalidOperationException("DataSource not initialized.");
        return _dataSource.CreateConnection();
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_dataSource == null) return false;
        try
        {
            using var conn = _dataSource.CreateConnection();
            await conn.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for {Name}", Name);
            return false;
        }
    }

    public async Task<List<SpatialTable>> GetTablesAsync()
    {
        using var conn = CreateConnection();
        // Same SQL logic as PostGISConnectionService
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
            return fallbackResult
                .Where(t => t.GeometryType.Contains("geo", StringComparison.OrdinalIgnoreCase) || t.GeometryType == "USER-DEFINED")
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query tables for {Name}", Name);
            return new List<SpatialTable>();
        }
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

    public async Task<BoundingBox> GetLayerExtentAsync(string schema, string table)
    {
        // ... Reusing logic from PostGISConnectionService ...
        using var conn = CreateConnection();
        string Quote(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

        var geomCol = await conn.ExecuteScalarAsync<string>(
            "SELECT f_geometry_column FROM geometry_columns WHERE f_table_schema = @Schema AND f_table_name = @Table",
            new { Schema = schema, Table = table });
            
        if (geomCol == null)
        {
             // Fallback
             var fallbackSql = @"
                SELECT column_name 
                FROM information_schema.columns
                WHERE table_schema = @Schema AND table_name = @Table
                  AND (udt_name IN ('geometry', 'geography') OR data_type = 'USER-DEFINED')";
             geomCol = await conn.ExecuteScalarAsync<string>(fallbackSql, new { Schema = schema, Table = table });
        }

        if (geomCol == null) throw new ArgumentException($"Not a spatial table: {schema}.{table}");

        int srid = 0;
        try { srid = await conn.ExecuteScalarAsync<int>($"SELECT srid FROM geometry_columns WHERE f_table_schema = '{schema}' AND f_table_name = '{table}'"); } catch {}
        if (srid == 0) try { srid = await conn.ExecuteScalarAsync<int>($"SELECT ST_SRID({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1"); } catch {}
        
        if (srid == 0) srid = GuessSrid(schema, table, geomCol, conn).Result;

        string geomExpr = $"ST_SetSRID({Quote(geomCol)}, {srid})"; 
        var sql = $"SELECT ST_XMin(ext) as MinX, ST_YMin(ext) as MinY, ST_XMax(ext) as MaxX, ST_YMax(ext) as MaxY FROM (SELECT ST_Extent(ST_Transform({geomExpr}, 4326)) as ext FROM {Quote(schema)}.{Quote(table)}) as t";
        
        var result = await conn.QueryFirstOrDefaultAsync(sql);
        var row = (IDictionary<string, object>?)result;
        if (row != null && row.ContainsKey("minx") && row["minx"] != null)
        {
            return new BoundingBox((double)row["minx"], (double)row["miny"], (double)row["maxx"], (double)row["maxy"]);
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

    public async Task<IEnumerable<Geometry>> GetGeometriesAsync(string schema, string table, BoundingBox bbox, IEnumerable<string>? properties = null, double pixelSize = 0)
    {
        using var conn = CreateConnection();
        string Quote(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

        var spatialInfo = await conn.QueryFirstOrDefaultAsync<(string? GeometryColumn, int Srid)>(
            "SELECT f_geometry_column, srid FROM geometry_columns WHERE f_table_schema = @Schema AND f_table_name = @Table",
            new { Schema = schema, Table = table });
            
        string? geomCol = spatialInfo.GeometryColumn;
        int srid = spatialInfo.Srid;

        if (string.IsNullOrEmpty(geomCol))
        {
             var fallbackSql = @"
                SELECT column_name 
                FROM information_schema.columns
                WHERE table_schema = @Schema AND table_name = @Table
                  AND (udt_name IN ('geometry', 'geography') OR data_type = 'USER-DEFINED')";
             geomCol = await conn.ExecuteScalarAsync<string>(fallbackSql, new { Schema = schema, Table = table });
        }
        if (string.IsNullOrEmpty(geomCol)) return new List<Geometry>();

        if (srid == 0) try { srid = await conn.ExecuteScalarAsync<int>($"SELECT ST_SRID({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1"); } catch {}
        if (srid == 0) srid = await GuessSrid(schema, table, geomCol, conn);

        string geomExpr = Quote(geomCol);
        
        // Prepare properties selection
        var selectParts = new List<string> { };
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                selectParts.Add($"{Quote(prop)}"); // Select as is
            }
        }
        string propSelect = selectParts.Count > 0 ? ", " + string.Join(", ", selectParts) : "";
        
        double width = bbox.MaxX - bbox.MinX;
        double buffer = width * 0.05;

        string finalGeomExpr = geomExpr;
        if (pixelSize > 0)
        {
            finalGeomExpr = $"ST_SimplifyPreserveTopology({geomExpr}, {pixelSize})";
        }

        string sql;
        if (srid == 3857 || srid == 900913)
        {
             sql = @$"
                SELECT {finalGeomExpr} as geom {propSelect}
                FROM {Quote(schema)}.{Quote(table)}
                WHERE ST_Intersects(
                    {Quote(geomCol)}, 
                    ST_MakeEnvelope(@MinX - @Buffer, @MinY - @Buffer, @MaxX + @Buffer, @MaxY + @Buffer, 3857)
                )";
        }
        else
        {
             string sourceGeom = $"ST_SetSRID({Quote(geomCol)}, {srid})"; 
             string transformed = $"ST_Transform({sourceGeom}, 3857)";
             string selectGeom = pixelSize > 0 ? $"ST_SimplifyPreserveTopology({transformed}, {pixelSize})" : transformed;

             sql = @$"
                SELECT {selectGeom} as geom {propSelect}
                FROM {Quote(schema)}.{Quote(table)}
                WHERE ST_Intersects(
                    ST_Transform({sourceGeom}, 3857), 
                    ST_MakeEnvelope(@MinX - @Buffer, @MinY - @Buffer, @MaxX + @Buffer, @MaxY + @Buffer, 3857)
                )";
        }

        var result = await conn.QueryAsync<dynamic>(sql, new { bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY, Buffer = buffer });
        
        var geometryList = new List<Geometry>();
        foreach (var row in result)
        {
            if (row.geom is Geometry g)
            {
                if (properties != null) 
                {
                    var dict = new Dictionary<string, object>();
                    var rowData = (IDictionary<string, object>)row; // Dapper result is IDictionary<string, object>

                    foreach (var prop in properties)
                    {
                         if (rowData.TryGetValue(prop, out var val) && val != null)
                         {
                             dict[prop] = val;
                         }
                    }
                    g.UserData = dict;
                }
                geometryList.Add(g);
            }
        }
        return geometryList;
    }

    private async Task<int> GuessSrid(string schema, string table, string geomCol, IDbConnection conn)
    {
        string Quote(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";
        try 
        {
             var xMin = await conn.ExecuteScalarAsync<double?>($"SELECT ST_XMin({Quote(geomCol)}) FROM {Quote(schema)}.{Quote(table)} LIMIT 1");
             if (xMin.HasValue && xMin.Value >= -180.0 && xMin.Value <= 180.0) return 4326;
             else if (xMin.HasValue && Math.Abs(xMin.Value) > 10_000_000 && Math.Abs(xMin.Value) < 20_037_508) return 3857;
             else return 5179;
        }
        catch { return 5179; }
    }

    private GeometryType ParseGeometryType(string? type)
    {
        if (type == null) return GeometryType.Unknown;
        if (Enum.TryParse<GeometryType>(type, true, out var result)) return result;
        if (type.StartsWith("POINT")) return GeometryType.Point;
        if (type.StartsWith("LINESTRING")) return GeometryType.LineString;
        if (type.StartsWith("POLYGON")) return GeometryType.Polygon;
        if (type.StartsWith("MULTIPOINT")) return GeometryType.MultiPoint;
        if (type.StartsWith("MULTILINESTRING")) return GeometryType.MultiLineString;
        if (type.StartsWith("MULTIPOLYGON")) return GeometryType.MultiPolygon;
        return GeometryType.Unknown;
    }

    public async Task<List<string>> GetUniqueValuesAsync(string schema, string table, string column)
    {
        using var conn = CreateConnection();
        string Quote(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";
        
        // Limit to 100 distinctive values to avoid performance hit
        var sql = $"SELECT DISTINCT {Quote(column)}::text FROM {Quote(schema)}.{Quote(table)} LIMIT 100";
        try
        {
             var result = await conn.QueryAsync<string>(sql);
             return result.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch unique values for {Schema}.{Table}.{Column}", schema, table, column);
            return new List<string>();
        }
    }

    public void Dispose()
    {
        _dataSource?.Dispose();
    }
}
