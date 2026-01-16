namespace SpatialTileBuilder.Core.Interfaces;

using System;
using System.Threading.Tasks;

public interface ITileWriter : IDisposable
{
    Task InitializeAsync(string outputPath);
    Task WriteTileAsync(int z, int x, int y, byte[] data);
    Task FinalizeAsync();
}
