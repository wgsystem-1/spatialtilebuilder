namespace SpatialTileBuilder.Infrastructure.Services;

using SpatialTileBuilder.Core.Entities;
using SpatialTileBuilder.Core.Interfaces;

public class SessionContext : ISessionContext
{
    public string? CurrentToken { get; set; }
    public User? CurrentUser { get; set; }
}
