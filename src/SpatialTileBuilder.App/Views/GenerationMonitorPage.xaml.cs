namespace SpatialTileBuilder.App.Views;

using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SpatialTileBuilder.App.ViewModels;

public partial class GenerationMonitorPage : Page
{
    public GenerationMonitorViewModel ViewModel { get; } = null!;

    public GenerationMonitorPage()
    { ViewModel = ((App)Application.Current).Services.GetService<GenerationMonitorViewModel>()!;
        DataContext = this;
        InitializeComponent();
    }
}
