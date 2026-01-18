namespace SpatialTileBuilder.Core.DTOs;

using System.Collections.Generic;

public class MapnikStyle
{
    public string Name { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public string Srs { get; set; } = string.Empty;
    public List<StyleLayer> Layers { get; set; } = new();
    public List<StyleDefinition> Styles { get; set; } = new();
}

public class StyleLayer
{
    public string Name { get; set; } = string.Empty;
    public string Srs { get; set; } = string.Empty;
    public List<string> StyleNames { get; set; } = new();
    public StyleDatasource? Datasource { get; set; }
}

public class StyleDatasource
{
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public class StyleDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<MapnikStyleRule> Rules { get; set; } = new();
}

public class MapnikStyleRule
{
    public string? Filter { get; set; }
    public double? MaxScaleDenominator { get; set; }
    public double? MinScaleDenominator { get; set; }
    public List<Symbolizer> Symbolizers { get; set; } = new();
}

public abstract class Symbolizer
{
    public string Kind { get; protected set; } = "Unknown";
}

public class PointSymbolizer : Symbolizer
{
    public string File { get; set; } = string.Empty;
    public PointSymbolizer() { Kind = "Point"; }
}

public class LineSymbolizer : Symbolizer
{
    public string Stroke { get; set; } = string.Empty;
    public double StrokeWidth { get; set; }
    public LineSymbolizer() { Kind = "Line"; }
}

public class PolygonSymbolizer : Symbolizer
{
    public string Fill { get; set; } = string.Empty;
    public double FillOpacity { get; set; } = 1.0;
    public PolygonSymbolizer() { Kind = "Polygon"; }
}

public class TextSymbolizer : Symbolizer
{
    public string FaceName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Fill { get; set; } = string.Empty;
    public TextSymbolizer() { Kind = "Text"; }
}
