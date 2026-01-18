namespace SpatialTileBuilder.Core.DTOs;

using System.Collections.Generic;

public record StyleConfiguration(
    List<LayerStyleConfig> Layers
);

public record LayerStyleConfig(
    string TableName,
    bool IsVisible,
    string FillColor,
    double Opacity,
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
    double PointSize
);
