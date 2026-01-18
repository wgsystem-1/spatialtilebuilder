using System.Windows.Controls;
using System.Windows.Input;
using SpatialTileBuilder.App.ViewModels;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace SpatialTileBuilder.App.Views.Components;

public partial class MapCanvasView : UserControl
{
    public MapCanvasView()
    {
        InitializeComponent();
        
        SkiaElement.PaintSurface += OnPaintSurface;
        SkiaElement.MouseDown += OnMouseDown;
        SkiaElement.MouseMove += OnMouseMove;
        SkiaElement.MouseUp += OnMouseUp;
        SkiaElement.MouseWheel += OnMouseWheel;

        this.DataContextChanged += (s, e) => 
        {
            if (DataContext is MapCanvasViewModel vm)
            {
                vm.InvalidateCanvas = () => SkiaElement.InvalidateVisual();
            }
        };
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MapCanvasViewModel vm)
        {
            vm.OnMouseDown(e.GetPosition(SkiaElement));
            SkiaElement.CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is MapCanvasViewModel vm)
        {
            vm.OnMouseMove(e.GetPosition(SkiaElement));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MapCanvasViewModel vm)
        {
            vm.OnMouseUp();
            SkiaElement.ReleaseMouseCapture();
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is MapCanvasViewModel vm)
        {
            vm.OnMouseWheel(e.Delta);
        }
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is MapCanvasViewModel vm)
        {
            if (vm.PaintSurfaceCommand.CanExecute(e))
                vm.PaintSurfaceCommand.Execute(e);
        }
    }
}
