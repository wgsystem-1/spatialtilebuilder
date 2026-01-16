namespace SpatialTileBuilder.Core.DTOs;

using System;
using System.Collections.Generic;

public record GenerationOptions(
    string OutputPath,
    string OutputFormat, // xyz, tms, mbtiles
    int MinZoom,
    int MaxZoom,
    BoundingBox Bbox,
    bool Overwrite,
    int ThreadCount,
    List<LayerStyle> Layers
);

public record GenerationProgress(
    long CompletedTiles,
    long TotalTiles,
    long FailedTiles,
    TimeSpan Elapsed,
    TimeSpan EstimatedRemaining,
    double TilesPerSecond
);

public record GenerationResult(
    bool IsSuccess,
    long TotalTiles,
    long FailedTiles,
    TimeSpan Duration
);
