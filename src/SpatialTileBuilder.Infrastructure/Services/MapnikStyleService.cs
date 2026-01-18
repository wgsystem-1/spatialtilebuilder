namespace SpatialTileBuilder.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;

public class MapnikStyleService : IMapnikStyleService
{
    private readonly ILogger<MapnikStyleService> _logger;

    public MapnikStyleService(ILogger<MapnikStyleService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MapnikStyle> LoadStyleAsync(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("Style file not found.", xmlPath);

        _logger.LogInformation("Loading style from {XmlPath}", xmlPath);

        // Run on thread pool since XDocument.Load is synchronous IO (partially)
        return await Task.Run(() => 
        {
            var doc = XDocument.Load(xmlPath);
            var mapElement = doc.Element("Map");
            if (mapElement == null) throw new InvalidOperationException("Invalid Mapnik XML: Root <Map> element missing.");

            var style = new MapnikStyle
            {
                BackgroundColor = mapElement.Attribute("background-color")?.Value ?? "#ffffff",
                Srs = mapElement.Attribute("srs")?.Value ?? "+proj=longlat +ellps=WGS84 +datum=WGS84 +no_defs"
            };

            // Parse Styles
            foreach (var styleElem in mapElement.Elements("Style"))
            {
                var styleDef = new StyleDefinition
                {
                    Name = styleElem.Attribute("name")?.Value ?? "Unnamed"
                };

                foreach (var ruleElem in styleElem.Elements("Rule"))
                {
                    var rule = new MapnikStyleRule
                    {
                        Filter = ruleElem.Element("Filter")?.Value,
                        MinScaleDenominator = (double?)ruleElem.Element("MinScaleDenominator"),
                        MaxScaleDenominator = (double?)ruleElem.Element("MaxScaleDenominator")
                    };
                    
                    // Parse Symbolizers (Simple implementation)
                    foreach (var sym in ruleElem.Elements())
                    {
                        if (sym.Name.LocalName == "LineSymbolizer")
                        {
                            rule.Symbolizers.Add(new LineSymbolizer 
                            { 
                                Stroke = sym.Attribute("stroke")?.Value ?? "#000000",
                                StrokeWidth = (double?)sym.Attribute("stroke-width") ?? 1.0
                            });
                        }
                        else if (sym.Name.LocalName == "PolygonSymbolizer")
                        {
                            rule.Symbolizers.Add(new PolygonSymbolizer 
                            { 
                                Fill = sym.Attribute("fill")?.Value ?? "#808080",
                                FillOpacity = (double?)sym.Attribute("fill-opacity") ?? 1.0
                            });
                        }
                        // Add more symbolizers as needed
                    }
                    styleDef.Rules.Add(rule);
                }
                style.Styles.Add(styleDef);
            }

            // Parse Layers
            foreach (var layerElem in mapElement.Elements("Layer"))
            {
                var layer = new StyleLayer
                {
                    Name = layerElem.Attribute("name")?.Value ?? "Unnamed",
                    Srs = layerElem.Attribute("srs")?.Value ?? ""
                };

                foreach (var styleName in layerElem.Elements("StyleName"))
                {
                    layer.StyleNames.Add(styleName.Value);
                }

                var dsElem = layerElem.Element("Datasource");
                if (dsElem != null)
                {
                    layer.Datasource = new StyleDatasource();
                    foreach (var param in dsElem.Elements("Parameter"))
                    {
                        var name = param.Attribute("name")?.Value;
                        if (name != null)
                        {
                            layer.Datasource.Parameters[name] = param.Value;
                        }
                    }
                }
                style.Layers.Add(layer);
            }

            return style;
        });
    }

    public List<StyleLayer> GetLayers(MapnikStyle style)
    {
        return style.Layers;
    }

    public bool ValidateStyle(MapnikStyle style, List<SpatialTable> tables)
    {
        if (style == null) return false;
        
        // Check if all layers have valid datasources matching selected tables
        // This is a simplified validation
        foreach (var layer in style.Layers)
        {
            if (layer.Datasource == null) continue;
            
            // Check table parameter
            if (layer.Datasource.Parameters.TryGetValue("table", out var tableQuery))
            {
                // Simple check: does the query contain any of the table names?
                // In reality, this needs SQL parsing or loose matching
            }
        }

        return true;
    }

    public string GenerateDefaultStyle(List<SpatialTable> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<Map srs=\"+proj=merc +a=6378137 +b=6378137 +lat_ts=0.0 +lon_0=0.0 +x_0=0.0 +y_0=0.0 +k=1.0 +units=m +nadgrids=@null +wktext +no_defs\" background-color=\"#ffffff\">");
        
        foreach (var table in tables)
        {
            var styleName = $"{table.Table}_style";
            
            // Style Definition
            sb.AppendLine($"  <Style name=\"{styleName}\">");
            sb.AppendLine("    <Rule>");
            
            if (table.GeometryType.Contains("Point", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <PointSymbolizer />"); // Basic point
            }
            else if (table.GeometryType.Contains("Line", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <LineSymbolizer stroke=\"#000000\" stroke-width=\"1\" />");
            }
            else if (table.GeometryType.Contains("Polygon", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <PolygonSymbolizer fill=\"#d3d3d3\" fill-opacity=\"0.5\" />");
                sb.AppendLine("      <LineSymbolizer stroke=\"#808080\" stroke-width=\"0.5\" />");
            }
            
            sb.AppendLine("    </Rule>");
            sb.AppendLine("  </Style>");
            
            // Layer Definition
            sb.AppendLine($"  <Layer name=\"{table.Table}\" srs=\"+proj=longlat +ellps=WGS84 +datum=WGS84 +no_defs\">");
            sb.AppendLine($"    <StyleName>{styleName}</StyleName>");
            sb.AppendLine("    <Datasource>");
            sb.AppendLine("       <Parameter name=\"type\">postgis</Parameter>");
            sb.AppendLine($"       <Parameter name=\"table\">(SELECT * FROM \"{table.Schema}\".\"{table.Table}\") as data</Parameter>");
            sb.AppendLine($"       <Parameter name=\"geometry_field\">{table.GeometryColumn}</Parameter>");
            // Other params usually injected at runtime or pre-filled
            sb.AppendLine("    </Datasource>");
            sb.AppendLine("  </Layer>");
        }
        
        sb.AppendLine("</Map>");
        return sb.ToString();
    }
}
