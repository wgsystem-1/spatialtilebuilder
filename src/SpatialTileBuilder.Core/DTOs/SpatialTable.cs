namespace SpatialTileBuilder.Core.DTOs;

public class SpatialTable
{
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string GeometryColumn { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public int Srid { get; set; }

    public SpatialTable() { }

    public SpatialTable(string schema, string table, string geometryColumn, string geometryType, int srid)
    {
        Schema = schema;
        Table = table;
        GeometryColumn = geometryColumn;
        GeometryType = geometryType;
        Srid = srid;
    }
}
