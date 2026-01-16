namespace SpatialTileBuilder.Core.Interfaces;

using System.Collections.Generic;
using System.Threading.Tasks;
using SpatialTileBuilder.Core.DTOs;

public interface IMapnikStyleService
{
    Task<MapnikStyle> LoadStyleAsync(string xmlPath);
    List<StyleLayer> GetLayers(MapnikStyle style);
    bool ValidateStyle(MapnikStyle style, List<SpatialTable> tables);
    string GenerateDefaultStyle(List<SpatialTable> tables);
}
