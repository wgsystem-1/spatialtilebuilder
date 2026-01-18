namespace SpatialTileBuilder.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;
using SpatialTileBuilder.Core.Interfaces;
using System.Linq;

public partial class RegionSelectionViewModel : ObservableObject
{
    private readonly ITileGridService _tileGridService;

    [ObservableProperty]
    private RegionType _selectedRegionType = RegionType.Full;

    // Sido
    [ObservableProperty]
    private string _selectedSido = string.Empty;
    public ObservableCollection<string> SidoList { get; } = new()
    {
        "Seoul", "Busan", "Daegu", "Incheon", "Gwangju", "Daejeon", "Ulsan", "Sejong",
        "Gyeonggi-do", "Gangwon-do", "Chungcheongbuk-do", "Chungcheongnam-do",
        "Jeollabuk-do", "Jeollanam-do", "Gyeongsangbuk-do", "Gyeongsangnam-do", "Jeju-do"
    };

    // BBox
    [ObservableProperty] private string _minX = "124.0";
    [ObservableProperty] private string _minY = "33.0";
    [ObservableProperty] private string _maxX = "132.0";
    [ObservableProperty] private string _maxY = "43.0";

    // Polygon
    [ObservableProperty]
    private string _polygonFilePath = string.Empty;

    // Zoom Levels
    [ObservableProperty] private int _minZoom = 0;
    [ObservableProperty] private int _maxZoom = 10;

    // Estimation
    [ObservableProperty] private long _estimatedTileCount;
    [ObservableProperty] private string _estimationMessage = string.Empty;

    // Threads
    [ObservableProperty] private int _threadCount = System.Environment.ProcessorCount;

    private readonly SpatialTileBuilder.App.Services.ProjectStateService _stateService;

    public RegionSelectionViewModel(ITileGridService tileGridService, SpatialTileBuilder.App.Services.ProjectStateService stateService)
    {
        _tileGridService = tileGridService;
        _stateService = stateService;
    }

    partial void OnSelectedRegionTypeChanged(RegionType value)
    {
        // Reset or adjust inputs based on type
        CalculateEstimationCommand.Execute(null);
    }

    private NetTopologySuite.Geometries.Geometry? _loadedRegionShape;

    [RelayCommand]
    private void BrowsePolygon()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
             Filter = "Spatial Files (*.shp;*.geojson;*.json)|*.shp;*.geojson;*.json|All Files (*.*)|*.*",
             Title = "Select Region Polygon"
        };

        if (dialog.ShowDialog() == true)
        {
            PolygonFilePath = dialog.FileName;
            LoadPolygonFile(PolygonFilePath);
        }
    }

    private void LoadPolygonFile(string path)
    {
        try
        {
            NetTopologySuite.Geometries.Geometry? geometry = null;
            var ext = System.IO.Path.GetExtension(path).ToLower();

            if (ext == ".geojson" || ext == ".json")
            {
                var reader = new NetTopologySuite.IO.GeoJsonReader();
                var json = System.IO.File.ReadAllText(path);
                geometry = reader.Read<NetTopologySuite.Geometries.Geometry>(json);
            }
            else if (ext == ".shp")
            {
                // Shapefile reading using NetTopologySuite.IO.ShapeFile
                var factory = new NetTopologySuite.Geometries.GeometryFactory();
                using var reader = new NetTopologySuite.IO.ShapefileDataReader(path, factory);
                
                var geoms = new List<NetTopologySuite.Geometries.Geometry>();
                while (reader.Read())
                {
                    geoms.Add(reader.Geometry);
                }
                
                if (geoms.Count == 1) geometry = geoms[0];
                else if (geoms.Count > 1) geometry = factory.CreateGeometryCollection(geoms.ToArray()).Union();
            }

            if (geometry != null)
            {
                _loadedRegionShape = geometry;
                // Calculate envelope (WGS84 assumed)
                var env = geometry.EnvelopeInternal;
                MinX = env.MinX.ToString();
                MinY = env.MinY.ToString();
                MaxX = env.MaxX.ToString();
                MaxY = env.MaxY.ToString();
                
                EstimationMessage = "Polygon loaded successfully. BBox updated.";
                // Trigger estimation?
                CalculateEstimationCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
             EstimationMessage = $"Failed to load polygon: {ex.Message}";
             _loadedRegionShape = null;
        }
    }

    // Format
    [ObservableProperty] private string _selectedOutputFormat = "xyz";
    public List<string> OutputFormats { get; } = new() { "xyz", "mbtiles" };

    [RelayCommand]
    private void CalculateEstimation()
    {
        try
        {
            BoundingBox bbox = GetCurrentBbox();
            
            // Refined total calculation if polygon is present?
            // Currently TileGridService.CalculateTotalTiles is simplistic (rectangle).
            // We'll trust it as an upper bound.
            
            EstimatedTileCount = _tileGridService.CalculateTotalTiles(bbox, MinZoom, MaxZoom);
            EstimationMessage = $"Estimated: {EstimatedTileCount:N0} tiles";
            
            // Adjust path based on format
            string defaultPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SpatialTiles");
            string output = defaultPath;
            if (SelectedOutputFormat == "mbtiles")
            {
                 output = System.IO.Path.Combine(defaultPath, "output.mbtiles");
            }

            // Save to shared state
            _stateService.Options = new GenerationOptions(
                OutputPath: output,
                OutputFormat: SelectedOutputFormat,
                MinZoom: MinZoom,
                MaxZoom: MaxZoom,
                Bbox: bbox,
                Overwrite: true,
                ThreadCount: ThreadCount,
                Layers: _stateService.StyledLayers.Any() 
                    ? new(_stateService.StyledLayers) 
                    : new(_stateService.SelectedLayers.Select(l => new LayerConfig(
                        Id: Guid.NewGuid().ToString(),
                        Name: l.Table.Table,
                        DataSourceId: "",
                        SourceName: l.Table.Schema + "." + l.Table.Table,
                        IsVisible: true,
                        Opacity: 0.8,
                        FillColor: "#ADD8E6",
                        IsFillVisible: true,
                        StrokeColor: "#808080",
                        StrokeWidth: 1.0,
                        StrokeDashArray: "Solid",
                        LabelColumn: "",
                        LabelSize: 12,
                        LabelColor: "#000000",
                        LabelHaloRadius: 0,
                        FontName: "Arial",
                        PointColor: "#FF0000",
                        PointSize: 5,
                        Rules: null
                      ))),
                RegionShape: (SelectedRegionType == RegionType.Polygon) ? _loadedRegionShape : null
            );
        }
        catch (Exception ex)
        {
            EstimationMessage = $"Error: {ex.Message}";
            EstimatedTileCount = 0;
        }
    }

    private BoundingBox GetCurrentBbox()
    {
        switch (SelectedRegionType)
        {
            case RegionType.Full:
                // Korea Approximate BBox
                return new BoundingBox(124.0, 33.0, 132.0, 43.0);
            
            case RegionType.Sido:
                // TODO: Look up Sido BBox from a dictionary/service
                return new BoundingBox(126.7, 37.4, 127.3, 37.7); // Dummy Seoul
            
            case RegionType.BBox:
                if (double.TryParse(MinX, out double minX) &&
                    double.TryParse(MinY, out double minY) &&
                    double.TryParse(MaxX, out double maxX) &&
                    double.TryParse(MaxY, out double maxY))
                {
                    return new BoundingBox(minX, minY, maxX, maxY);
                }
                throw new ArgumentException("Invalid coordinates");

            case RegionType.Polygon:
                // TODO: Parse polygon file and get extent
                return new BoundingBox(124.0, 33.0, 132.0, 43.0); 

            default:
                return new BoundingBox(0,0,0,0);
        }
    }
}
