namespace SpatialTileBuilder.Infrastructure.Services;

using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Entities;
using SpatialTileBuilder.Core.Interfaces;

public sealed class AuthService : IAuthService
{
    private readonly ILocalDatabase _database;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ILocalDatabase database,
        ISessionContext sessionContext,
        ILogger<AuthService> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        using var conn = _database.CreateConnection();
        
        // Use explicit aliases to ensure Dapper maps correctly
        var sql = @"
            SELECT 
                id, 
                username, 
                password_hash AS PasswordHash, 
                role, 
                created_at AS CreatedAt, 
                last_login AS LastLogin 
            FROM users 
            WHERE username = @Username";

        var user = await conn.QuerySingleOrDefaultAsync<User>(sql, new { Username = username });

        if (user == null)
        {
            _logger.LogWarning("Login failed: User {Username} not found.", username);
            return new AuthResult(false, null, "Invalid username or password");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            _logger.LogError("Login failed: Password hash is empty for user {Username}. Mapping issue?", username);
            return new AuthResult(false, null, "Authentication error: internal configuration issue.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: Invalid password for user {Username}.", username);
            return new AuthResult(false, null, "Invalid username or password");
        }

        // Generate Token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert.ToBase64String(tokenBytes);

        // Insert Session
        var expiresAt = DateTime.UtcNow.AddHours(24);
        await conn.ExecuteAsync(@"
            INSERT INTO user_sessions (user_id, token, expires_at) 
            VALUES (@UserId, @Token, @ExpiresAt)",
            new { UserId = user.Id, Token = token, ExpiresAt = expiresAt });

        // Update Last Login
        await conn.ExecuteAsync(
            "UPDATE users SET last_login = @LastLogin WHERE id = @Id",
            new { LastLogin = DateTime.UtcNow, Id = user.Id });

        // Update Context
        _sessionContext.CurrentToken = token;
        _sessionContext.CurrentUser = user;

        _logger.LogInformation("User {Username} logged in.", username);
        return new AuthResult(true, token, null);
    }

    public async Task<bool> ValidateSessionAsync(string token)
    {
        using var conn = _database.CreateConnection();
        var session = await conn.QuerySingleOrDefaultAsync<UserSession>(
            "SELECT * FROM user_sessions WHERE token = @Token", new { Token = token });

        if (session == null) return false;

        if (session.ExpiresAt < DateTime.UtcNow)
        {
             await conn.ExecuteAsync("DELETE FROM user_sessions WHERE id = @Id", new { session.Id });
             return false;
        }

        return true;
    }

    public async Task LogoutAsync(string token)
    {
        using var conn = _database.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM user_sessions WHERE token = @Token", new { Token = token });
        
        if (_sessionContext.CurrentToken == token)
        {
            _sessionContext.CurrentToken = null;
            _sessionContext.CurrentUser = null;
        }
        _logger.LogInformation("Session logged out.");
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (_sessionContext.CurrentUser != null)
            return _sessionContext.CurrentUser;

        var token = _sessionContext.CurrentToken;
        if (string.IsNullOrEmpty(token)) return null;

        using var conn = _database.CreateConnection();
        
        var sql = @"
            SELECT u.* 
            FROM users u
            JOIN user_sessions s ON u.id = s.user_id
            WHERE s.token = @Token AND s.expires_at > @Now";
        
        var user = await conn.QuerySingleOrDefaultAsync<User>(sql, new { Token = token, Now = DateTime.UtcNow });

        if (user != null)
        {
            _sessionContext.CurrentUser = user;
        }

        return user;
    }
}
