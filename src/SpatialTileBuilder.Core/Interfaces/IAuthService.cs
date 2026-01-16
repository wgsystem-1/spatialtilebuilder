namespace SpatialTileBuilder.Core.Interfaces;

using System.Threading.Tasks;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Entities;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<bool> ValidateSessionAsync(string token);
    Task LogoutAsync(string token);
    Task<User?> GetCurrentUserAsync();
}
