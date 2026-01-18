using System.Windows;
using SpatialTileBuilder.App.ViewModels;

namespace SpatialTileBuilder.App.Views;

public partial class MainWindow : Window
{
    public MainWorkspaceViewModel MainWorkspaceViewModel { get; }

    public MainWindow(MainWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        MainWorkspaceViewModel = viewModel;
        DataContext = this; // Or set DataContext directly to ViewModel if View expects it
    }
}
