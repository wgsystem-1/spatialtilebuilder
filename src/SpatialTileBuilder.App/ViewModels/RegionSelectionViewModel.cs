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

    [RelayCommand]
    private void BrowsePolygon()
    {
        // TODO: Implement file picker dialog through a service or code-behind interaction
        // For now, placeholder
        PolygonFilePath = "Start picking file..."; 
    }

    [RelayCommand]
    private void CalculateEstimation()
    {
        try
        {
            BoundingBox bbox = GetCurrentBbox();
            
            EstimatedTileCount = _tileGridService.CalculateTotalTiles(bbox, MinZoom, MaxZoom);
            EstimationMessage = $"Estimated: {EstimatedTileCount:N0} tiles";
            
            // Save to shared state
            _stateService.Options = new GenerationOptions(
                OutputPath: System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SpatialTiles"), // Default path
                OutputFormat: "xyz",
                MinZoom: MinZoom,
                MaxZoom: MaxZoom,
                Bbox: bbox,
                Overwrite: true,
                ThreadCount: 4,
                Layers: _stateService.StyledLayers.Any() 
                    ? new(_stateService.StyledLayers) 
                    : new(_stateService.SelectedLayers.Select(l => new LayerStyle(
                        l.Table, true, "#ADD8E6", 0.8, true, "#808080", 1.0, "Solid", "", 12, "#000000", 0, "#FF0000", 5
                      )))
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
