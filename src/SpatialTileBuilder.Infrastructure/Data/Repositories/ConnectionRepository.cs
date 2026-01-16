namespace SpatialTileBuilder.Infrastructure.Data.Repositories;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using SpatialTileBuilder.Core.Entities;
using SpatialTileBuilder.Core.Interfaces;

public class ConnectionRepository : IConnectionRepository
{
    private readonly ILocalDatabase _database;

    public ConnectionRepository(ILocalDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IEnumerable<DbConnection>> GetAllAsync()
    {
        using var conn = _database.CreateConnection();
        return await conn.QueryAsync<DbConnection>("SELECT * FROM db_connections ORDER BY name");
    }

    public async Task<DbConnection?> GetAsync(int id)
    {
        using var conn = _database.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<DbConnection>("SELECT * FROM db_connections WHERE id = @Id", new { Id = id });
    }

    public async Task<int> AddAsync(DbConnection connection)
    {
        using var conn = _database.CreateConnection();
        var sql = @"
            INSERT INTO db_connections (name, host, port, database, username, password_encrypted, ssl_mode, created_at, updated_at)
            VALUES (@Name, @Host, @Port, @Database, @Username, @PasswordEncrypted, @SslMode, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";
        
        connection.CreatedAt = DateTime.UtcNow;
        connection.UpdatedAt = DateTime.UtcNow;
        
        return await conn.ExecuteScalarAsync<int>(sql, connection);
    }

    public async Task UpdateAsync(DbConnection connection)
    {
        using var conn = _database.CreateConnection();
        var sql = @"
            UPDATE db_connections 
            SET name = @Name, host = @Host, port = @Port, database = @Database, username = @Username, 
                password_encrypted = @PasswordEncrypted, ssl_mode = @SslMode, updated_at = @UpdatedAt
            WHERE id = @Id";

        connection.UpdatedAt = DateTime.UtcNow;
        await conn.ExecuteAsync(sql, connection);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _database.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM db_connections WHERE id = @Id", new { Id = id });
    }

    public async Task<bool> ExistsAsync(string name)
    {
        using var conn = _database.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM db_connections WHERE name = @Name", new { Name = name });
        return count > 0;
    }
}
