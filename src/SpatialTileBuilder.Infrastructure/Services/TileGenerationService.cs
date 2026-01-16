namespace SpatialTileBuilder.Infrastructure.Services;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using SpatialTileBuilder.Infrastructure.Tiles; // For writers

public class TileGenerationService : ITileGenerationService
{
    private readonly ITileGridService _tileGridService;
    private readonly IMapnikRenderer _renderer;
    private readonly ILogger<TileGenerationService> _logger;
    private readonly ManualResetEventSlim _pauseEvent = new(true);

    public TileGenerationService(
        ITileGridService tileGridService,
        IMapnikRenderer renderer,
        ILogger<TileGenerationService> logger)
    {
        _tileGridService = tileGridService;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        IProgress<GenerationProgress> progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting tile generation...");
        _pauseEvent.Set(); // Ensure started

        var stopwatch = Stopwatch.StartNew();
        long totalTiles = _tileGridService.CalculateTotalTiles(options.Bbox, options.MinZoom, options.MaxZoom);
        long completedCount = 0;
        long failedCount = 0;

        // Ensure renderer knows which layers to render
        if (options.Layers != null)
        {
            _renderer.SetLayers(options.Layers);
        }

        // Initialize Writer
        using ITileWriter writer = options.OutputFormat.ToLower() switch
        {
            "xyz" => new XyzTileWriter(),
            _ => new XyzTileWriter() // Default
        };

        await writer.InitializeAsync(options.OutputPath);

        // Generate Tile List (lazy)
        var tiles = _tileGridService.GetTilesInBbox(options.Bbox, options.MinZoom); 
        // Note: Currently GetTilesInBbox only does one zoom level. 
        // We need to iterate all zoom levels.
        var allTiles = Enumerable.Empty<TileIndex>();
        for (int z = options.MinZoom; z <= options.MaxZoom; z++)
        {
            allTiles = allTiles.Concat(_tileGridService.GetTilesInBbox(options.Bbox, z));
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.ThreadCount > 0 ? options.ThreadCount : Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(allTiles, parallelOptions, async (tile, ct) =>
            {
                _pauseEvent.Wait(CancellationToken.None); // Handle Pause

                try
                {
                    // Render (Simulated delay in Mock)
                    byte[] data = _renderer.RenderTile(tile.Z, tile.X, tile.Y);
                    
                    // Write
                    await writer.WriteTileAsync(tile.Z, tile.X, tile.Y, data);

                    Interlocked.Increment(ref completedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed tile {Z}/{X}/{Y}", tile.Z, tile.X, tile.Y);
                    Interlocked.Increment(ref failedCount);
                }

                // Progress Report (throttled)
                if (completedCount % 10 == 0)
                {
                    ReportProgress(progress, completedCount, totalTiles, failedCount, stopwatch.Elapsed);
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Generation cancelled.");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await writer.FinalizeAsync();
        }

        return new GenerationResult(true, completedCount, failedCount, stopwatch.Elapsed);
    }

    private void ReportProgress(
        IProgress<GenerationProgress> progress, 
        long completed, 
        long total, 
        long failed, 
        TimeSpan elapsed)
    {
        double tps = completed / elapsed.TotalSeconds;
        TimeSpan remaining = tps > 0 ? TimeSpan.FromSeconds((total - completed) / tps) : TimeSpan.Zero;
        
        progress.Report(new GenerationProgress(
            completed,
            total,
            failed,
            elapsed,
            remaining,
            tps
        ));
    }

    public void Pause()
    {
        _pauseEvent.Reset();
    }

    public void Resume()
    {
        _pauseEvent.Set();
    }
}
