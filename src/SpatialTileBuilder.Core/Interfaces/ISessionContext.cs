namespace SpatialTileBuilder.Core.Interfaces;

using SpatialTileBuilder.Core.Entities;

public interface ISessionContext
{
    string? CurrentToken { get; set; }
    User? CurrentUser { get; set; }
}
