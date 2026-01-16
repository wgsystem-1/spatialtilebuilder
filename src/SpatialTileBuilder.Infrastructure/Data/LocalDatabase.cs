namespace SpatialTileBuilder.Infrastructure.Data;

using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.Interfaces;

/// <summary>
/// Implementation of ILocalDatabase using SQLite
/// </summary>
public sealed class LocalDatabase : ILocalDatabase
{
    private readonly string _connectionString;
    private readonly ILogger<LocalDatabase> _logger;

    public LocalDatabase(ILogger<LocalDatabase> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Dapper logic to match snake_case columns with PascalCase properties
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SpatialTileBuilder");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        var dbPath = Path.Combine(folder, "app.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Local Database...");

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            // Users Table
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    role TEXT NOT NULL DEFAULT 'operator' CHECK (role IN ('admin', 'operator', 'viewer')),
                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    last_login DATETIME
                );
                CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
            ", transaction: transaction);

            // User Sessions Table
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS user_sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    token TEXT UNIQUE NOT NULL,
                    expires_at DATETIME NOT NULL,
                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS idx_sessions_token ON user_sessions(token);
                CREATE INDEX IF NOT EXISTS idx_sessions_user ON user_sessions(user_id);
            ", transaction: transaction);

            // App Settings Table
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT,
                    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ", transaction: transaction);

            // DB Connections Table
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS db_connections (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL,
                    host TEXT NOT NULL,
                    port INTEGER NOT NULL,
                    database TEXT NOT NULL,
                    username TEXT NOT NULL,
                    password_encrypted BLOB NOT NULL,
                    ssl_mode TEXT DEFAULT 'Prefer',
                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS idx_connections_name ON db_connections(name);
            ", transaction: transaction);

            // 마이그레이션: db_connections 테이블에 updated_at 컬럼이 있는지 확인 (기존 데이터베이스 호환성)
            var hasUpdatedAt = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('db_connections') WHERE name='updated_at'", 
                transaction: transaction);
            
            if (hasUpdatedAt == 0)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE db_connections ADD COLUMN updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP", 
                    transaction: transaction);
                _logger.LogInformation("Migrated db_connections: added updated_at column.");
            }

            // 관리자 계정 시딩
            var adminExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM users WHERE username = 'admin'", transaction: transaction);
            
            if (adminExists == 0)
            {
                // Password: admin123
                var hash = BCrypt.Net.BCrypt.HashPassword("admin123"); 
                await connection.ExecuteAsync(@"
                    INSERT INTO users (username, password_hash, role) 
                    VALUES ('admin', @Hash, 'admin')", 
                    new { Hash = hash }, transaction: transaction);
                
                _logger.LogInformation("Seeded admin user.");
            }

            // Seed Settings
            var settingsExists = await connection.ExecuteScalarAsync<int>(
               "SELECT COUNT(1) FROM app_settings WHERE key = 'app_version'", transaction: transaction);

            if (settingsExists == 0) {
                await connection.ExecuteAsync(@"
                   INSERT INTO app_settings (key, value) VALUES 
                   ('app_version', '0.1.0'),
                   ('db_schema_version', '1'),
                   ('theme', 'light'),
                   ('language', 'ko'),
                   ('max_parallel_threads', '8'),
                   ('tile_buffer_size', '64'),
                   ('log_level', 'INFO'),
                   ('log_retention_days', '30');
                ", transaction: transaction);
                 _logger.LogInformation("Seeded app settings.");
            }

            transaction.Commit();
            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
    }
}
