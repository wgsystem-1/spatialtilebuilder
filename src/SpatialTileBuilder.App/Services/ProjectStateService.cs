namespace SpatialTileBuilder.App.Services;

using System.Collections.Generic;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.App.ViewModels; // For SelectableSpatialTable

public class ProjectStateService
{
    public GenerationOptions? Options { get; set; }
    public List<SelectableSpatialTable> SelectedLayers { get; set; } = new();
    public List<LayerConfig> StyledLayers { get; set; } = new();
}
