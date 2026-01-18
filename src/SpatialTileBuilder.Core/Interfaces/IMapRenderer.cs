namespace SpatialTileBuilder.Core.Interfaces;

using SkiaSharp;
using SpatialTileBuilder.Core.DTOs;
using System.Threading.Tasks;

public interface IMapRenderer
{
    Task UpdateDataAsync(ProjectConfiguration project, double width, double height); // Calculate BBox internally based on project
    void Draw(SKCanvas canvas, double width, double height);
}
