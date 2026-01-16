using System.Windows;
using SpatialTileBuilder.App.Views;

namespace SpatialTileBuilder.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(new LoginPage());
    }
}
