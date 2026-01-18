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
            "mbtiles" => new MBTilesWriter(), // No DI for logger here, simple instantiation
            _ => new XyzTileWriter() // Default
        };

        await writer.InitializeAsync(options.OutputPath);

        // Generate Tiles Enumerable
        IEnumerable<TileIndex> TileSource()
        {
            for (int z = options.MinZoom; z <= options.MaxZoom; z++)
            {
                IEnumerable<TileIndex> tilesLayer;
                if (options.RegionShape != null)
                {
                    tilesLayer = _tileGridService.GetTilesInPolygon(options.RegionShape, z);
                }
                else
                {
                    tilesLayer = _tileGridService.GetTilesInBbox(options.Bbox, z);
                }

                foreach (var t in tilesLayer) yield return t;
            }
        }
        
        long totalTiles = 0;
        // Calculating total count might be expensive if fully enumerated. 
        // For progress, we ideally need total. If TileGridService has specialized Count methods, that's best.
        // For now, let's keep it simple: We might not know total accurately if we stream, OR we double iterate (one for count, one for process).
        // Since OOM is the concern, double iteration is better than materializing list.
        // Or we can just estimate or count first.
        
        // Calculate Total Count without materializing
        foreach(var _ in TileSource()) totalTiles++;

        // Update logger with actual count
        _logger.LogInformation($"Total tiles to generate: {totalTiles}");

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.ThreadCount > 0 ? options.ThreadCount : Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        try
        {
            // Stream processing
            await Parallel.ForEachAsync(TileSource(), parallelOptions, async (tile, ct) =>
            {
                _pauseEvent.Wait(CancellationToken.None); // Handle Pause

                try
                {
                    // Render (Simulated delay in Mock)
                    byte[]? data = _renderer.RenderTile(tile.Z, tile.X, tile.Y);
                    
                    if (data != null && data.Length > 0)
                    {
                        // Write only if data exists
                        await writer.WriteTileAsync(tile.Z, tile.X, tile.Y, data);
                    }

                    Interlocked.Increment(ref completedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed tile {Z}/{X}/{Y}", tile.Z, tile.X, tile.Y);
                    Interlocked.Increment(ref failedCount);
                }

                // Progress Report (throttled)
                var current = Interlocked.Read(ref completedCount);
                if (current % 10 == 0 || current == totalTiles)
                {
                    ReportProgress(progress, current, totalTiles, failedCount, stopwatch.Elapsed);
                }
            });
            
            // Final progress report to ensure 100%
            ReportProgress(progress, totalTiles, totalTiles, failedCount, stopwatch.Elapsed);
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
