namespace SpatialTileBuilder.Core.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using SpatialTileBuilder.Core.DTOs;

public interface ITileGenerationService
{
    Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        IProgress<GenerationProgress> progress,
        CancellationToken cancellationToken);
    
    void Pause();
    void Resume();
}
