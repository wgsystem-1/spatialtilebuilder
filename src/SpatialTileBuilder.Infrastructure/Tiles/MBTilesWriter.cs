namespace SpatialTileBuilder.Infrastructure.Tiles;

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SpatialTileBuilder.Core.Interfaces;
using Microsoft.Extensions.Logging;

public class MBTilesWriter : ITileWriter
{
    private string _connectionString = string.Empty;
    private SqliteConnection? _connection;
    private readonly ILogger<MBTilesWriter> _logger;

    public MBTilesWriter(ILogger<MBTilesWriter> logger)
    {
        _logger = logger;
    }

    // Default constructor for simple DI if logger is optional, but better to inject
    public MBTilesWriter() 
    {
        // Manual logger not ideal but acceptable for factory usage if needed
    }

    public async Task InitializeAsync(string outputPath)
    {
        var dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

        // Ensure extension
        if (!outputPath.EndsWith(".mbtiles", StringComparison.OrdinalIgnoreCase))
            outputPath += ".mbtiles";

        _connectionString = $"Data Source={outputPath}";
        
        // Re-create if exists (since we usually Overwrite=true in this app)
        // Or we could handle Append. But for now, let's assume new unique generation.
        // Actually, SQLite Create table IF NOT EXISTS is safer.

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        // Tune performance
        using var cmdConfig = _connection.CreateCommand();
        cmdConfig.CommandText = @"
            PRAGMA synchronous=OFF;
            PRAGMA journal_mode=MEMORY;
        ";
        await cmdConfig.ExecuteNonQueryAsync();

        // Schema
        using var cmdSchema = _connection.CreateCommand();
        cmdSchema.CommandText = @"
            CREATE TABLE IF NOT EXISTS metadata (name text, value text);
            CREATE TABLE IF NOT EXISTS tiles (zoom_level integer, tile_column integer, tile_row integer, tile_data blob);
            CREATE UNIQUE INDEX IF NOT EXISTS tile_index on tiles (zoom_level, tile_column, tile_row);
        ";
        await cmdSchema.ExecuteNonQueryAsync();

        // Clear existing if overwriting
        // For now, let's just clear
        using var cmdClear = _connection.CreateCommand();
        cmdClear.CommandText = "DELETE FROM metadata; DELETE FROM tiles;";
        await cmdClear.ExecuteNonQueryAsync();
    }

    public async Task WriteTileAsync(int z, int x, int y, byte[] data)
    {
        if (_connection == null) return;

        // XYZ to TMS conversion for MBTiles
        // TMS y = (2^z - 1) - y
        long tmsY = (long)(Math.Pow(2, z) - 1) - y;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO tiles (zoom_level, tile_column, tile_row, tile_data) VALUES (@z, @x, @y, @data)";
        cmd.Parameters.AddWithValue("@z", z);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", tmsY);
        cmd.Parameters.AddWithValue("@data", data);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task FinalizeAsync()
    {
        if (_connection == null) return;

        try 
        {
            // Metadata
            using var tx = _connection.BeginTransaction();
            
            using var cmdMeta = _connection.CreateCommand();
            cmdMeta.Transaction = tx;
            cmdMeta.CommandText = @"
                INSERT INTO metadata (name, value) VALUES ('name', 'SpatialTileBuilder Layer');
                INSERT INTO metadata (name, value) VALUES ('format', 'png');
                INSERT INTO metadata (name, value) VALUES ('type', 'overlay');
                INSERT INTO metadata (name, value) VALUES ('version', '1.0');
            ";
            await cmdMeta.ExecuteNonQueryAsync();
            
            await tx.CommitAsync();
        }
        catch(Exception ex) 
        {
             // Log?
             Console.WriteLine($"MBTiles Finalize Error: {ex.Message}");
        }
        finally
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
