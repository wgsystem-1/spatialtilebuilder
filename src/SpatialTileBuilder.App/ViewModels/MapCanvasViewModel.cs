namespace SpatialTileBuilder.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using SkiaSharp.Views.Desktop;
using SpatialTileBuilder.App.Services;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;
using System;
using System.Windows;
using System.Windows.Input;

public partial class MapCanvasViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly IMapRenderer _renderer;

    // View State
    [ObservableProperty] private double _zoomLevel = 12.0;
    [ObservableProperty] private double _cntX = 127.0; // Longitude
    [ObservableProperty] private double _cntY = 37.5;  // Latitude

    private bool _isDragging = false;
    private Point _lastMousePos;
    
    // Canvas Size (updated by View)
    public double CanvasWidth { get; set; } = 800;
    public double CanvasHeight { get; set; } = 450;

    public MapCanvasViewModel(ProjectService projectService, IMapRenderer renderer)
    {
        _projectService = projectService;
        _renderer = renderer;

        if (_projectService.CurrentProject != null)
        {
            CntX = _projectService.CurrentProject.CenterX;
            CntY = _projectService.CurrentProject.CenterY;
            ZoomLevel = _projectService.CurrentProject.Zoom;
        }

        // Subscribe to Project changes
        _projectService.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(ProjectService.CurrentProject))
            {
                 // Project changed entirely (load new project)
                 // Update local state if needed
                 if (_projectService.CurrentProject != null)
                 {
                    CntX = _projectService.CurrentProject.CenterX;
                    CntY = _projectService.CurrentProject.CenterY;
                    ZoomLevel = _projectService.CurrentProject.Zoom;
                 }
                 await RefreshDataAsync();
            }
        };
        
        // Initial Load
        RefreshDataAsync();
    }

    public Action InvalidateCanvas { get; set; }

    // Trigger data reload
    private async System.Threading.Tasks.Task RefreshDataAsync()
    {
        if (_projectService.CurrentProject == null) return;
        
        // Update Project Config with current view state
        // (Actually ProjectService should hold the source of truth, but let's sync)
        var project = _projectService.CurrentProject with { 
            CenterX = CntX, 
            CenterY = CntY, 
            Zoom = (int)ZoomLevel 
        };

        await _renderer.UpdateDataAsync(project, CanvasWidth, CanvasHeight);
        InvalidateCanvas?.Invoke();
    }

    public void Refresh()
    {
        RefreshDataAsync();
    }

    [RelayCommand]
    private void PaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        
        // Update size tracking
        CanvasWidth = info.Width;
        CanvasHeight = info.Height;

        // Draw Cached Data
        _renderer.Draw(canvas, info.Width, info.Height);
        
        // Draw debug text
        /*
        using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 20 };
        canvas.ResetMatrix();
        canvas.DrawText($"Zoom: {ZoomLevel:F2}, Center: {CntX:F4}, {CntY:F4}", 10, 30, textPaint);
        */
    }

    public void OnMouseDown(Point p)
    {
        _isDragging = true;
        _lastMousePos = p;
    }

    public void OnMouseMove(Point p)
    {
        if (_isDragging)
        {
            double dx = p.X - _lastMousePos.X;
            double dy = p.Y - _lastMousePos.Y;

            // Pan Logic
            double n = Math.Pow(2, ZoomLevel);
            
            // Current Center Pixel
            var currentPx = LatLonToPixel(CntY, CntX, ZoomLevel);
            var newPx = new Point(currentPx.X - dx, currentPx.Y - dy);
            
            var newLatLon = PixelToLatLon(newPx.X, newPx.Y, ZoomLevel);
            
            // Update properties (will trigger notifications, but we want to batch redraws?)
            // SetProperty field directly if loop
            _cntX = newLatLon.X;
            _cntY = newLatLon.Y;
            OnPropertyChanged(nameof(CntX));
            OnPropertyChanged(nameof(CntY));

            _lastMousePos = p;
            
            // For dragging, maybe just Invalidate for smooth pan (re-render cached data with offset), 
            // but our renderer currently draws based on "Last Project State".
            // So we MUST call RefreshDataAsync to update renderer's view of the world.
            // BUT RefreshDataAsync fetches data which is slow.
            // OPTIMIZATION:
            // Renderer.Draw should take cx, cy, zoom overrides!
            // Let's keep it simple: Just call Invalidate. The Renderer.Draw uses _lastCx... 
            // Wait, Renderer.Draw uses stored state. We need to update Renderer's Viewport state without fetching data.
            // Since we changed interface to UpdateDataAsync (fetch) + Draw.
            // We need a way to "UpdateViewportOnly".
            // For MVP, just RefreshDataAsync (Performance might suffer).
            // Better: update UpdateDataAsync to accept "fetch" flag?
            // Actually, for PANing small amounts, we should just shift the canvas if data covers enough area.
            // Let's just call RefreshDataAsync. If Provider caches, it's fast.
            // Since PostGISProvider fetches by BBox, panning changes BBox, so query is needed.
            RefreshDataAsync(); 
        }
    }

    public void OnMouseUp()
    {
        _isDragging = false;
        // Maybe trigger high-res load here
    }

    public void OnMouseWheel(int delta)
    {
        double zoomDelta = delta > 0 ? 0.5 : -0.5;
        ZoomLevel = Math.Clamp(ZoomLevel + zoomDelta, 1, 20);
        RefreshDataAsync();
    }

    // Helper Methods (Web Mercator) - Keep for Mouse Logic
    private Point LatLonToPixel(double lat, double lon, double zoom)
    {
        lat = Math.Clamp(lat, -85.05112878, 85.05112878);
        double n = Math.Pow(2, zoom);
        double x = (lon + 180.0) / 360.0 * 256.0 * n;
        double latRad = lat * Math.PI / 180.0;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * 256.0 * n;
        return new Point(x, y);
    }

    private Point PixelToLatLon(double x, double y, double zoom)
    {
        double n = Math.Pow(2, zoom);
        double lonDeg = x / (256.0 * n) * 360.0 - 180.0;
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / (256.0 * n))));
        double latDeg = latRad * 180.0 / Math.PI;
        return new Point(lonDeg, latDeg);
    }
}
