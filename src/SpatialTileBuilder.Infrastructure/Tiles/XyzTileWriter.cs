namespace SpatialTileBuilder.Infrastructure.Tiles;

using System;
using System.IO;
using System.Threading.Tasks;
using SpatialTileBuilder.Core.Interfaces;

public class XyzTileWriter : ITileWriter
{
    private string _outputPath = string.Empty;

    public Task InitializeAsync(string outputPath)
    {
        _outputPath = outputPath;
        if (!Directory.Exists(_outputPath))
        {
            Directory.CreateDirectory(_outputPath);
        }
        return Task.CompletedTask;
    }

    public async Task WriteTileAsync(int z, int x, int y, byte[] data)
    {
        // Format: /{z}/{x}/{y}.png
        var dir = Path.Combine(_outputPath, z.ToString(), x.ToString());
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = Path.Combine(dir, $"{y}.png");
        await File.WriteAllBytesAsync(path, data);
    }

    public Task FinalizeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
