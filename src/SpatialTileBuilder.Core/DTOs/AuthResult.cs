namespace SpatialTileBuilder.Core.DTOs;

public record AuthResult(bool IsSuccess, string? Token, string? ErrorMessage);
