using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using SpatialTileBuilder.Core.DTOs;

namespace SpatialTileBuilder.App.ViewModels;

public partial class StyleLayerViewModel : ObservableObject
{
    public SpatialTable TableInfo { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _fillColor = "#ADD8E6"; // LightBlue

    [ObservableProperty]
    private double _opacity = 0.8;

    [ObservableProperty]
    private bool _isFillVisible = true;

    // Stroke
    [ObservableProperty]
    private string _strokeColor = "#808080"; // Gray

    [ObservableProperty]
    private double _strokeWidth = 1.0;

    [ObservableProperty]
    private string _strokeDashArray = "Solid"; // Solid, Dash, Dot

    public System.Collections.Generic.List<string> DashStyles { get; } = new() { "Solid", "Dash", "Dot", "DashDot" };

    // Label
    [ObservableProperty]
    private System.Collections.Generic.List<string> _columns = new();

    [ObservableProperty]
    private string _selectedLabelColumn = string.Empty;

    [ObservableProperty]
    private double _labelSize = 12.0;

    [ObservableProperty]
    private string _labelColor = "#000000";

    [ObservableProperty]
    private double _labelHaloRadius = 0.0; // 0 = no halo

    // Point
    [ObservableProperty]
    private string _pointColor = "#FF0000";

    [ObservableProperty]
    private double _pointSize = 5.0;

    public StyleLayerViewModel(SpatialTable table)
    {
        TableInfo = table;
        _name = table.Table;
    }
}
