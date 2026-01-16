namespace SpatialTileBuilder.Core.Entities;

using System;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "operator"; // admin, operator, viewer
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
