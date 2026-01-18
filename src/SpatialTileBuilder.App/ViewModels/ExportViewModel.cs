namespace SpatialTileBuilder.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class ExportViewModel : ObservableObject
{
    private readonly ITileGenerationService _generationService;
    private readonly Services.ProjectService _projectService;
    private readonly ILogger<ExportViewModel> _logger;
    private CancellationTokenSource? _cts;

    // Configuration
    [ObservableProperty] private string _outputFilePath = string.Empty;
    [ObservableProperty] private int _minZoom = 0;
    [ObservableProperty] private int _maxZoom = 14;
    [ObservableProperty] private double _minX;
    [ObservableProperty] private double _minY;
    [ObservableProperty] private double _maxX;
    [ObservableProperty] private double _maxY;

    // State
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _statsText = string.Empty;

    public ExportViewModel(
        ITileGenerationService generationService,
        Services.ProjectService projectService,
        ILogger<ExportViewModel> logger)
    {
        _generationService = generationService;
        _projectService = projectService;
        _logger = logger;

        // Default constraints
        // Initialize with rough world bounds or project center
        MinX = -180; MaxX = 180; MinY = -85; MaxY = 85; 
    }

    public void Initialize(BoundingBox currentViewExtent)
    {
        MinX = currentViewExtent.MinX;
        MinY = currentViewExtent.MinY;
        MaxX = currentViewExtent.MaxX;
        MaxY = currentViewExtent.MaxY;
        // Adjust zoom based on current view?
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "MBTiles file (*.mbtiles)|*.mbtiles",
            FileName = "output.mbtiles"
        };
        if (dialog.ShowDialog() == true)
        {
            OutputFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task StartExportAsync()
    {
        if (string.IsNullOrEmpty(OutputFilePath))
        {
            StatusText = "Please select an output file.";
            return;
        }

        if (IsGenerating) return;

        IsGenerating = true;
        IsPaused = false;
        ProgressValue = 0;
        StatusText = "Initializing...";

        _cts = new CancellationTokenSource();
        var progress = new Progress<GenerationProgress>(OnProgressUpdate);

        // Build Options
        var options = new GenerationOptions(
            OutputFilePath,
            "mbtiles", // OutputFormat
            MinZoom,
            MaxZoom,
            new BoundingBox(MinX, MinY, MaxX, MaxY),
            true, // Overwrite
            0, // ThreadCount (Default)
            _projectService.CurrentProject.Layers // Pass current layers
        );

        try
        {
            var result = await _generationService.GenerateAsync(options, progress, _cts.Token);
            StatusText = result.IsSuccess ? "Export Complete!" : "Export Failed.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Export Cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _logger.LogError(ex, "Export failed");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsGenerating) _cts?.Cancel();
    }

    private void OnProgressUpdate(GenerationProgress p)
    {
        if (p.TotalTiles > 0)
        {
            ProgressValue = (double)p.CompletedTiles / p.TotalTiles * 100.0;
        }
        StatusText = $"Processing: {p.CompletedTiles} / {p.TotalTiles}";
        StatsText = $"TPS: {p.TilesPerSecond:F1} | Failed: {p.FailedTiles}";
    }
}
