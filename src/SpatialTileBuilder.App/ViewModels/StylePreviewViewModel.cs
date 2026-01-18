namespace SpatialTileBuilder.App.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.Interfaces;

public partial class StylePreviewViewModel : ObservableObject
{
    private readonly IMapnikRenderer _renderer;
    private readonly IMapnikStyleService _styleService;
    private readonly IPostGISConnectionService _connectionService;
    private readonly SpatialTileBuilder.App.Services.ProjectStateService _stateService;

    public ObservableCollection<int> ZoomLevels { get; } = new();

    [ObservableProperty]
    private string _styleFilePath = string.Empty;

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    private int _selectedZoom = 12;

    private int _tileX;
    private int _tileY;

    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<StyleLayerViewModel> Layers { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLayerSelected))]
    private StyleLayerViewModel? _selectedLayer;

    public bool IsLayerSelected => SelectedLayer != null;

    public StylePreviewViewModel(
        IMapnikRenderer renderer,
        IMapnikStyleService styleService,
        IPostGISConnectionService connectionService,
        SpatialTileBuilder.App.Services.ProjectStateService stateService)
    {
        _renderer = renderer;
        _styleService = styleService;
        _connectionService = connectionService;
        _stateService = stateService;

        for (int i = 0; i <= 20; i++) ZoomLevels.Add(i);

        // Auto-initialize if state exists
        if (_stateService.SelectedLayers.Any())
        {
            Initialize(_stateService.SelectedLayers);
        }
    }

    [RelayCommand]
    private async Task MoveLayerUp()
    {
        if (SelectedLayer == null) return;
        int idx = Layers.IndexOf(SelectedLayer);
        if (idx > 0)
        {
            Layers.Move(idx, idx - 1);
            await UpdateStyleAndRefreshAsync();
        }
    }

    [RelayCommand]
    private async Task MoveLayerDown()
    {
        if (SelectedLayer == null) return;
        int idx = Layers.IndexOf(SelectedLayer);
        if (idx < Layers.Count - 1)
        {
            Layers.Move(idx, idx + 1);
            await UpdateStyleAndRefreshAsync();
        }
    }

    [RelayCommand]
    private async Task UpdateStyleAndRefreshAsync()
    {
        var mapXml = GenerateDefaultMapnikXml(Layers.ToList());
        var tempFile = Path.Combine(Path.GetTempPath(), $"style_{DateTime.Now.Ticks}.xml");
        File.WriteAllText(tempFile, mapXml);
        StyleFilePath = tempFile;
        await LoadStyleAsync();
    }


    [RelayCommand]
    private void SaveStyleJson()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Style Config (*.json)|*.json",
            Title = "Save Style Configuration",
            FileName = "style_config.json"
        };
        // ... (rest of SaveStyle implementation)
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var config = new SpatialTileBuilder.Core.DTOs.StyleConfiguration(
                    Layers.Select(l => new SpatialTileBuilder.Core.DTOs.LayerStyleConfig(
                        l.TableInfo.Table,
                        l.IsVisible,
                        l.FillColor,
                        l.Opacity,
                        l.IsFillVisible,
                        l.StrokeColor,
                        l.StrokeWidth,
                        l.StrokeDashArray,
                        l.SelectedLabelColumn,
                        l.LabelSize,
                        l.LabelColor,
                        l.LabelHaloRadius,
                        l.FontName,
                        l.PointColor,
                        l.PointSize
                    )).ToList()
                );

                var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                StatusMessage = "Style saved successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save style: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void LoadStyleJson()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Style Config (*.json)|*.json",
            Title = "Load Style Configuration"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var config = System.Text.Json.JsonSerializer.Deserialize<SpatialTileBuilder.Core.DTOs.StyleConfiguration>(json);

                if (config != null && config.Layers != null)
                {
                    foreach (var layerConfig in config.Layers)
                    {
                        var targetLayer = Layers.FirstOrDefault(l => l.TableInfo.Table == layerConfig.TableName);
                        if (targetLayer != null)
                        {
                            targetLayer.IsVisible = layerConfig.IsVisible;
                            targetLayer.FillColor = layerConfig.FillColor;
                            targetLayer.Opacity = layerConfig.Opacity;
                            targetLayer.IsFillVisible = layerConfig.IsFillVisible;
                            targetLayer.StrokeColor = layerConfig.StrokeColor;
                            targetLayer.StrokeWidth = layerConfig.StrokeWidth;
                            targetLayer.StrokeDashArray = layerConfig.StrokeDashArray;
                            targetLayer.SelectedLabelColumn = layerConfig.LabelColumn ?? "";
                            targetLayer.LabelSize = layerConfig.LabelSize;
                            targetLayer.LabelColor = layerConfig.LabelColor;
                            targetLayer.LabelHaloRadius = layerConfig.LabelHaloRadius;
                            targetLayer.FontName = layerConfig.FontName;
                            targetLayer.PointColor = layerConfig.PointColor;
                            targetLayer.PointSize = layerConfig.PointSize;
                        }
                    }
                    StatusMessage = "Style loaded successfully.";
                    UpdateStyleAndRefreshAsync().ConfigureAwait(false); 
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load style: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task LoadStyleAsync()
    {
        if (string.IsNullOrEmpty(StyleFilePath)) return;

        IsLoading = true;
        try
        {
            // _renderer.LoadStyle(StyleFilePath); // Mock renderer ignores this, but real one would use it.
            // Pass the ORDERED LIST to the renderer with STYLES
            var styleDtos = Layers.Where(l => l.IsVisible).Select(l => new SpatialTileBuilder.Core.DTOs.LayerConfig(
                Id: l.TableInfo.Table,
                Name: l.TableInfo.Table,
                DataSourceId: "",
                SourceName: l.TableInfo.Schema + "." + l.TableInfo.Table,
                IsVisible: l.IsVisible,
                Opacity: l.Opacity,
                FillColor: l.FillColor,
                IsFillVisible: l.IsFillVisible,
                StrokeColor: l.StrokeColor,
                StrokeWidth: l.StrokeWidth,
                StrokeDashArray: l.StrokeDashArray,
                LabelColumn: l.SelectedLabelColumn,
                LabelSize: l.LabelSize,
                LabelColor: l.LabelColor,
                LabelHaloRadius: l.LabelHaloRadius,
                FontName: l.FontName,
                PointColor: l.PointColor,
                PointSize: l.PointSize,
                Rules: null
            )).ToList();

            _renderer.SetLayers(styleDtos);
            
            StatusMessage = $"Loaded {Layers.Count} layers.";
            await RefreshPreviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load style: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        IsLoading = true;
        try
        {
            // Use current tile coordinates (set during Initialize or Navigation)
            int z = SelectedZoom;
            int x = _tileX; 
            int y = _tileY;

            // Render on background thread
            byte[]? data = null;
            await Task.Run(() => 
            {
                data = _renderer.RenderTile(z, x, y);
            });

            if (data != null && data.Length > 0)
            {
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(data))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze(); // For thread safety
                }
                PreviewImage = bitmap;
                StatusMessage = $"Tile: {z}/{x}/{y} updated.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Render failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Pan(int dx, int dy)
    {
        _tileX += dx;
        _tileY += dy;
        // Don't await, fire and forget or let UI handle async
        _ = RefreshPreviewAsync();
    }

    public void Zoom(int delta)
    {
        int newZoom = Math.Clamp(SelectedZoom + delta, 0, 20);
        if (newZoom == SelectedZoom) return;
        SelectedZoom = newZoom; // This will trigger OnSelectedZoomChanged logic
    }

    // Temporary storage for zoom transition
    private double _lastCenterLon;
    private double _lastCenterLat;

    partial void OnSelectedZoomChanging(int value)
    {
        // Calculate center of current tile at current zoom
        var (lon, lat) = TileToWorldPos(_tileX + 0.5, _tileY + 0.5, SelectedZoom);
        _lastCenterLon = lon;
        _lastCenterLat = lat;
    }

    partial void OnSelectedZoomChanged(int value)
    {
        // Recalculate tile pos for new zoom at the same center
        var (tx, ty) = WorldToTilePos(_lastCenterLon, _lastCenterLat, value);
        _tileX = tx;
        _tileY = ty;
        
        // Refresh
        _ = RefreshPreviewAsync();
    }

    private (double lon, double lat) TileToWorldPos(double tx, double ty, int zoom)
    {
        int n = 1 << zoom; // 2^zoom
        double lon = (tx / n * 360.0) - 180.0;
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * ty / n)));
        double lat = latRad * 180.0 / Math.PI;
        return (lon, lat);
    }

    public event EventHandler? NextRequested;

    [RelayCommand]
    private void NavigateNext()
    {
        // Save styles to state
        var styles = Layers.Where(l => l.IsVisible).Select(l => new SpatialTileBuilder.Core.DTOs.LayerConfig(
            Id: Guid.NewGuid().ToString(),
            Name: l.TableInfo.Table,
            DataSourceId: "",
            SourceName: l.TableInfo.Schema + "." + l.TableInfo.Table,
            IsVisible: l.IsVisible,
            Opacity: l.Opacity,
            FillColor: l.FillColor,
            IsFillVisible: l.IsFillVisible,
            StrokeColor: l.StrokeColor,
            StrokeWidth: l.StrokeWidth,
            StrokeDashArray: l.StrokeDashArray,
            LabelColumn: l.SelectedLabelColumn,
            LabelSize: l.LabelSize,
            LabelColor: l.LabelColor,
            LabelHaloRadius: l.LabelHaloRadius,
            FontName: l.FontName,
            PointColor: l.PointColor,
            PointSize: l.PointSize,
            Rules: null
        )).ToList();
        
        _stateService.StyledLayers = styles;

        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Initialize(List<SelectableSpatialTable> layers)
    {
        if (layers == null || layers.Count == 0) return;

        Layers.Clear();
        foreach(var l in layers) Layers.Add(new StyleLayerViewModel(l.Table));
        SelectedLayer = Layers.FirstOrDefault();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!Layers.Any()) return;

        try 
        {
            // Populate columns
            foreach (var layer in Layers)
            {
                try 
                {
                    var cols = await _connectionService.GetColumnsAsync(layer.TableInfo.Schema, layer.TableInfo.Table);
                    layer.Columns = new List<string>(cols);
                } 
                catch (Exception ex)
                {
                    StatusMessage = $"Cols failed: {ex.Message}";
                }
            }

            await UpdateStyleAndRefreshAsync();

            // Calculate center tile based on FIRST layer
            try 
            {
                var first = Layers[0]; 
                var extent = await _connectionService.GetLayerExtentAsync(first.TableInfo.Schema, first.TableInfo.Table);
                // Check if extent is valid
                if (Math.Abs(extent.MinX) > 0.0001 || Math.Abs(extent.MaxX) > 0.0001)
                {
                    double cx = (extent.MinX + extent.MaxX) / 2.0;
                    double cy = (extent.MinY + extent.MaxY) / 2.0;
                    var (tx, ty) = WorldToTilePos(cx, cy, SelectedZoom);
                    _tileX = tx;
                    _tileY = ty;
                    StatusMessage = $"Ext: {extent.MinX:F2},{extent.MinY:F2} Tile: {tx},{ty}";
                }
                else
                {
                     // Default to Seoul if extent is invalid
                     var (tx, ty) = WorldToTilePos(127.0, 37.5, SelectedZoom);
                     _tileX = tx;
                     _tileY = ty;
                     StatusMessage = $"Invalid Extent. Defaulting to Seoul.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Extent calc failed: {ex.Message}";
            }
            
            // Auto load
            await RefreshPreviewAsync(); 
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to generate default style: {ex.Message}";
        }
    }

    private (int x, int y) WorldToTilePos(double lon, double lat, int zoom)
    {
        int n = (int)Math.Pow(2, zoom);
        double x = (lon + 180.0) / 360.0 * n;
        double latRad = lat * Math.PI / 180.0;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + (1 / Math.Cos(latRad))) / Math.PI) / 2.0 * n;
        return ((int)x, (int)y);
    }

    private string GenerateDefaultMapnikXml(List<StyleLayerViewModel> layers)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<Map srs=\"+proj=merc +a=6378137 +b=6378137 +lat_ts=0.0 +lon_0=0.0 +x_0=0.0 +y_0=0 +k=1.0 +units=m +nadgrids=@null +wktext +no_defs +over\">");
        sb.AppendLine("  <Parameters>");
        sb.AppendLine("    <Parameter name=\"bounds\">-20037508.34,-20037508.34,20037508.34,20037508.34</Parameter>");
        sb.AppendLine("  </Parameters>");

        var info = _connectionService.GetCurrentConnectionInfo();

        // Define a default style for each layer
        foreach (var layer in layers)
        {
            if (!layer.IsVisible) continue;

            var styleName = $"style_{layer.TableInfo.Table}";
            sb.AppendLine($"  <Style name=\"{styleName}\">");
            sb.AppendLine("    <Rule>");
            
            // Stroke
            sb.AppendLine($"      <LineSymbolizer stroke=\"{layer.StrokeColor}\" stroke-width=\"{layer.StrokeWidth}\" />");
            
            // Fill
            if (layer.IsFillVisible)
            {
                sb.AppendLine($"      <PolygonSymbolizer fill=\"{layer.FillColor}\" fill-opacity=\"{layer.Opacity}\" />");
            }

            // Points
            // Mapnik PointSymbolizer usually needs a file, using MarkersSymbolizer as proxy for simple circles in XML if supported or just standard Point
            sb.AppendLine($"      <MarkersSymbolizer fill=\"{layer.PointColor}\" width=\"{layer.PointSize}\" />");

            // Label
            // Label
            if (!string.IsNullOrEmpty(layer.SelectedLabelColumn))
            {
                 // Use selected font or default to DejaVu
                 string font = string.IsNullOrEmpty(layer.FontName) ? "DejaVu Sans Book" : layer.FontName;
                 sb.AppendLine($"      <TextSymbolizer face-name=\"{font}\" size=\"{layer.LabelSize}\" fill=\"{layer.LabelColor}\" halo-radius=\"{layer.LabelHaloRadius}\" placement=\"point\">[{layer.SelectedLabelColumn}]</TextSymbolizer>");
            }

            sb.AppendLine("    </Rule>");
            sb.AppendLine("  </Style>");
            
            string srs = layer.TableInfo.Srid > 0 ? $"+init=epsg:{layer.TableInfo.Srid}" : "+proj=longlat +datum=WGS84 +no_defs";
            sb.AppendLine($"  <Layer name=\"{layer.TableInfo.Table}\" srs=\"{srs}\">");
            sb.AppendLine($"    <StyleName>{styleName}</StyleName>");
            sb.AppendLine("    <Datasource>");
            sb.AppendLine("       <Parameter name=\"type\">postgis</Parameter>");
            
            if (info != null)
            {
                sb.AppendLine($"       <Parameter name=\"host\">{info.Host}</Parameter>");
                sb.AppendLine($"       <Parameter name=\"port\">{info.Port}</Parameter>");
                sb.AppendLine($"       <Parameter name=\"dbname\">{info.Database}</Parameter>");
                sb.AppendLine($"       <Parameter name=\"user\">{info.Username}</Parameter>");
                sb.AppendLine($"       <Parameter name=\"password\">{info.Password}</Parameter>");
                sb.AppendLine($"       <Parameter name=\"sslmode\">{info.SslMode.ToLower()}</Parameter>");
            }

            // Using subquery for table to handle schemas cleanly
            sb.AppendLine($"       <Parameter name=\"table\">(SELECT * FROM {layer.TableInfo.Schema}.{layer.TableInfo.Table}) as data</Parameter>");
            sb.AppendLine($"       <Parameter name=\"geometry_field\">{layer.TableInfo.GeometryColumn}</Parameter>");
            sb.AppendLine("       <Parameter name=\"estimate_extent\">true</Parameter>");
            sb.AppendLine("    </Datasource>");
            sb.AppendLine("  </Layer>");
        }
        sb.AppendLine("</Map>");
        return sb.ToString();
    }
}
