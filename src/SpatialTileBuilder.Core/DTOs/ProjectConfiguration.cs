namespace SpatialTileBuilder.Core.DTOs;

using System.Collections.Generic;

public record ProjectConfiguration(
    string ProjectName,
    List<DataSourceConfig> DataSources,
    List<LayerConfig> Layers,
    double CenterX, 
    double CenterY, 
    int Zoom
);
