namespace SpatialTileBuilder.Core.Entities;

using System;

public class DbConnection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public byte[] PasswordEncrypted { get; set; } = Array.Empty<byte>();
    public string SslMode { get; set; } = "prefer";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
