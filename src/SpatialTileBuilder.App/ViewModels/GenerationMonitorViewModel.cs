namespace SpatialTileBuilder.App.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using Microsoft.Extensions.Logging;

public partial class GenerationMonitorViewModel : ObservableObject
{
    private readonly ITileGenerationService _generationService;
    private readonly ILogger<GenerationMonitorViewModel> _logger;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private double _progressValue; // 0 to 100

    [ObservableProperty]
    private string _statusText = "Ready to start.";

    [ObservableProperty]
    private string _statsText = string.Empty;

    public ObservableCollection<string> Logs { get; } = new();

    private readonly SpatialTileBuilder.App.Services.ProjectStateService _stateService;

    public GenerationMonitorViewModel(
        ITileGenerationService generationService,
        ILogger<GenerationMonitorViewModel> logger,
        SpatialTileBuilder.App.Services.ProjectStateService stateService)
    {
        _generationService = generationService;
        _logger = logger;
        _stateService = stateService;
    }

    [RelayCommand]
    private async Task StartGenerationAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        IsPaused = false;
        ProgressValue = 0;
        Logs.Clear();
        Logs.Add("Starting generation...");
        StatusText = "Initializing...";

        _cts = new CancellationTokenSource();
        var progress = new Progress<GenerationProgress>(OnProgressUpdate);

        var options = _stateService.Options;
        if (options == null)
        {
            StatusText = "Please go to 'Set Region' and Estimate first.";
            Logs.Add("Error: No generation options set.");
            IsGenerating = false;
            return;
        }

        try
        {
            var result = await _generationService.GenerateAsync(options, progress, _cts.Token);
            
            StatusText = result.IsSuccess ? "Generation Complete!" : "Generation Failed.";
            Logs.Add($"Finished. Total: {result.TotalTiles}, Failed: {result.FailedTiles}, Duration: {result.Duration}");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Generation Cancelled.";
            Logs.Add("Cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = "Error occurred.";
            Logs.Add($"Error: {ex.Message}");
            _logger.LogError(ex, "Generation error");
        }
        finally
        {
            IsGenerating = false;
            IsPaused = false;
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (IsGenerating && !IsPaused)
        {
            _generationService.Pause();
            IsPaused = true;
            StatusText = "Paused.";
            Logs.Add("Paused.");
        }
    }

    [RelayCommand]
    private void Resume()
    {
        if (IsGenerating && IsPaused)
        {
            _generationService.Resume();
            IsPaused = false;
            StatusText = "Running...";
            Logs.Add("Resumed.");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsGenerating)
        {
            _cts?.Cancel();
        }
    }

    private void OnProgressUpdate(GenerationProgress p)
    {
        if (p.TotalTiles > 0)
        {
            ProgressValue = (double)p.CompletedTiles / p.TotalTiles * 100.0;
        }
        
        StatusText = $"Running... {p.CompletedTiles:N0} / {p.TotalTiles:N0}";
        StatsText = $"TPS: {p.TilesPerSecond:F1} | Rem: {p.EstimatedRemaining:hh\\:mm\\:ss} | Failed: {p.FailedTiles}";
        
        // Auto-scroll logs? Maybe too noisy to log every update.
    }
}
