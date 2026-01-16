namespace SpatialTileBuilder.App.Views;

using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SpatialTileBuilder.App.ViewModels;

public partial class LayerSelectionPage : Page
{
    public LayerSelectionViewModel ViewModel { get; }

    public LayerSelectionPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<LayerSelectionViewModel>();
        DataContext = this;
        InitializeComponent();
        
        ViewModel.NextRequested += (s, args) =>
        {
            // Navigate to StylePreviewPage, passing selected items
            var page = new StylePreviewPage();
            NavigationService?.Navigate(page);
            // Note: WPF doesn't support navigation parameters like WinUI3
            // You may need to pass data through ViewModel or other means
        };
    }
}