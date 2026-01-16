namespace SpatialTileBuilder.App.Views;

using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SpatialTileBuilder.App.ViewModels;

public partial class RegionSelectionPage : Page
{
    public RegionSelectionViewModel ViewModel { get; } = null!;

    public RegionSelectionPage()
    {
        ViewModel = ((App)Application.Current).Services.GetService<RegionSelectionViewModel>()!;
        DataContext = this;
        InitializeComponent();
    }
}
