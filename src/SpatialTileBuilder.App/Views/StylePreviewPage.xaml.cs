namespace SpatialTileBuilder.App.Views;

using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Collections.Generic;
using SpatialTileBuilder.App.ViewModels;

public partial class StylePreviewPage : Page
{
    public StylePreviewViewModel ViewModel { get; } = null!;

    public StylePreviewPage()
    {
        ViewModel = ((App)Application.Current).Services.GetService<StylePreviewViewModel>()!;
        DataContext = this;
        InitializeComponent();
        
        ViewModel.NextRequested += (s, e) => 
        {
            NavigationService?.Navigate(new RegionSelectionPage());
        };
    }

    private Point _lastMousePosition;
    private bool _isDragging;

    private void Border_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (e.Delta > 0) ViewModel.Zoom(1);
        else if (e.Delta < 0) ViewModel.Zoom(-1);
        e.Handled = true;
    }

    private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var border = sender as Border;
        if (border == null) return;

        border.CaptureMouse();
        _isDragging = true;
        _lastMousePosition = e.GetPosition(border);
    }

    private void Border_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var border = sender as Border;
        if (border == null) return;

        var currentPos = e.GetPosition(border);
        var offset = currentPos - _lastMousePosition;

        // Visual feedback
        if (this.ImgTranslate != null)
        {
             this.ImgTranslate.X += offset.X;
             this.ImgTranslate.Y += offset.Y;
        }

        _lastMousePosition = currentPos;
    }

    private void Border_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        
        var border = sender as Border;
        if (border != null) border.ReleaseMouseCapture();
        _isDragging = false;

        if (this.ImgTranslate != null)
        {
            double totalX = this.ImgTranslate.X;
            double totalY = this.ImgTranslate.Y;

            // Logic: Dragging Image Right (+X) means seeing what's on the Left (-TileX)
            // Threshold: If drag is significant (>50px), move tile.
            // But since we render 1 tile (256px), moving 1 tile requires ~256px drag?
            // Let's make it sensitive: >100px triggers move.
            // Or better: Accumulate fractional moves? Too complex for integers.
            // Just round.

            int tileDx = -(int)Math.Round(totalX / 256.0);
            int tileDy = -(int)Math.Round(totalY / 256.0);
            
            // If dragging text field (small), don't jump.
            // If tileDx is 0 but we dragged a lot (e.g. 200px), maybe forcing 1 step is better feel.
            if (tileDx == 0 && Math.Abs(totalX) > 100) tileDx = -(int)Math.Sign(totalX);
            if (tileDy == 0 && Math.Abs(totalY) > 100) tileDy = -(int)Math.Sign(totalY);

            if (tileDx != 0 || tileDy != 0)
            {
                ViewModel.Pan(tileDx, tileDy);
            }

            // Reset transform or keep it until render?
            // ViewModel.Pan is async. Visual feedback remains until new image loads?
            // Actually, if we reset immediately, image jumps back then new image loads.
            // It's a bit glitchy but acceptable for MVP.
            this.ImgTranslate.X = 0;
            this.ImgTranslate.Y = 0;
        }
    }
}
