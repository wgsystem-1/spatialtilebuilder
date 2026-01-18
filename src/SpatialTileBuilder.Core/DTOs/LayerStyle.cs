namespace SpatialTileBuilder.Core.DTOs;

public record LayerStyle(
    SpatialTable TableInfo,
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
