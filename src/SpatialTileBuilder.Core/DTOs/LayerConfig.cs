namespace SpatialTileBuilder.Core.DTOs;

using System.Collections.Generic;

public record LayerConfig(
    string Id,
    string Name,
    string DataSourceId, // Links to DataSourceConfig.Id
    string SourceName,   // Table Name (PostGIS) or File Name (Shapefile)
    bool IsVisible,
    double Opacity,
    
    // Legacy Style Properties (Flat structure for now, will be rule-based later)
    string FillColor,
    bool IsFillVisible,
    string StrokeColor,
    double StrokeWidth,
    string StrokeDashArray,
    string LabelColumn,
    double LabelSize,
    string LabelColor,
    double LabelHaloRadius,
    string FontName,
    string PointColor,
    double PointSize,
    // Optional list of rules MUST BE LAST
    System.Collections.Generic.List<StyleRule>? Rules = null 
);
