namespace SpatialTileBuilder.Core.Interfaces;

using System;

public interface IMapnikRenderer : IDisposable
{
    void LoadStyle(string xmlPath);
    void SetDatasource(string connectionString);
    void SetLayers(System.Collections.Generic.List<SpatialTileBuilder.Core.DTOs.LayerConfig> layers);
    byte[]? RenderTile(int z, int x, int y, int tileSize = 256);
}
