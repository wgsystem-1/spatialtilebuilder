namespace SpatialTileBuilder.Core.DTOs;

public record ConnectionInfo(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    string SslMode
);
