namespace SpatialTileBuilder.Core.Interfaces;

using System.Collections.Generic;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using SpatialTileBuilder.Core.DTOs;

public interface ILayerDataProvider
{
    string DataSourceId { get; }
    
    // For Vector Data (Fetches geometry + requested attributes in UserData)
    Task<IEnumerable<Geometry>> GetGeometriesAsync(BoundingBox bbox, int zoom, IEnumerable<string>? properties = null);
    
    // For Raster Data (returns raw image bytes, e.g. PNG/JPEG)
    Task<byte[]> GetImageBytesAsync(BoundingBox bbox, int width, int height);

    bool IsVector { get; }
}
