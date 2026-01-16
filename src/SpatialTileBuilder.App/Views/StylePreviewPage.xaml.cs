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
}
