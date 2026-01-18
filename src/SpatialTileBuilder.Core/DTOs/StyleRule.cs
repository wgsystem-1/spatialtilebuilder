namespace SpatialTileBuilder.Core.DTOs;

public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Contains,
    IsNull
}

public record FilterCondition(
    string ColumnName,
    FilterOperator Operator,
    string Value
);

public record StyleRule(
    string Name,
    FilterCondition? Filter, // Null filter means "Else" or "Default"
    string FillColor,
    string StrokeColor,
    double StrokeWidth,
    bool IsVisible = true
);
