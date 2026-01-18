using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SpatialTileBuilder.App.ViewModels;

public partial class LayerPropertiesViewModel : ObservableObject
{
    public IDataSourceService? DataSourceService { get; set; }
    private LayerConfig? _currentLayer;

    [ObservableProperty]
    private string _layerName = string.Empty;

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private bool _isFillVisible = true;

    [ObservableProperty]
    private Color _fillColor = Colors.LightGray;

    [ObservableProperty]
    private Color _strokeColor = Colors.Black;

    [ObservableProperty]
    private double _strokeWidth = 1.0;

    public ObservableCollection<StyleRuleViewModel> Rules { get; } = new();

    public LayerPropertiesViewModel(IDataSourceService? dataSourceService = null)
    {
        DataSourceService = dataSourceService;
    }

    public void LoadLayer(LayerConfig layer)
    {
        _currentLayer = layer;
        LayerName = layer.Name;
        Opacity = layer.Opacity;
        IsFillVisible = layer.IsFillVisible;
        
        try { FillColor = (Color)ColorConverter.ConvertFromString(layer.FillColor); } catch { }
        try { StrokeColor = (Color)ColorConverter.ConvertFromString(layer.StrokeColor); } catch { }
        
        StrokeWidth = layer.StrokeWidth;

        Rules.Clear();
        if (layer.Rules != null)
        {
            foreach (var rule in layer.Rules)
            {
                Rules.Add(new StyleRuleViewModel(rule));
            }
        }
    }

    public LayerConfig? GetUpdatedLayer()
    {
        if (_currentLayer == null) return null;

        return _currentLayer with
        {
            Name = LayerName,
            Opacity = Opacity,
            IsFillVisible = IsFillVisible,
            FillColor = FillColor.ToString(),
            StrokeColor = StrokeColor.ToString(),
            StrokeWidth = StrokeWidth,
            Rules = Rules.Select(r => r.ToDTO()).ToList()
        };
    }

    [RelayCommand]
    private void AddRule()
    {
        Rules.Add(new StyleRuleViewModel(new StyleRule("New Rule", null, "#FF0000", "#000000", 1.0)));
    }

    [RelayCommand]
    private void RemoveRule(StyleRuleViewModel rule)
    {
        Rules.Remove(rule);
    }

    [RelayCommand]
    public async Task ClassifyAsync(string column)
    {
        if (DataSourceService == null || _currentLayer == null) return;
        
        // Quick parsing fix:
        string schema = "public";
        string table = _currentLayer.SourceName;
        if (table.Contains("."))
        {
            var parts = table.Split('.');
            schema = parts[0];
            table = parts[1];
        }

        var values = await DataSourceService.GetUniqueValuesAsync(schema, table, column); 
        
        Rules.Clear();
        var random = new System.Random();
        
        foreach (var val in values)
        {
            var color = Color.FromRgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            var rule = new StyleRule(val, new FilterCondition(column, FilterOperator.Equals, val), color.ToString(), "#000000", 1.0);
            Rules.Add(new StyleRuleViewModel(rule));
        }
    }
}

public partial class StyleRuleViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _filterColumn;

    [ObservableProperty]
    private string _filterValue;

    [ObservableProperty]
    private Color _fillColor;

    public StyleRuleViewModel(StyleRule rule)
    {
        _name = rule.Name;
        if (rule.Filter != null)
        {
            _filterColumn = rule.Filter.ColumnName;
            _filterValue = rule.Filter.Value;
        }
        else
        {
            _filterColumn = "";
            _filterValue = "";
        }

        try { _fillColor = (Color)ColorConverter.ConvertFromString(rule.FillColor); } catch { _fillColor = Colors.Red; }
    }

    public StyleRule ToDTO()
    {
        FilterCondition? filter = null;
        if (!string.IsNullOrEmpty(FilterColumn))
        {
            filter = new FilterCondition(FilterColumn, FilterOperator.Equals, FilterValue);
        }

        return new StyleRule(Name, filter, FillColor.ToString(), "#000000", 1.0);
    }
}
