namespace SpatialTileBuilder.App.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;
using SpatialTileBuilder.App.ViewModels;
using System.ComponentModel;
using Serilog;

public partial class ConnectionWizardPage : Page
{
    public ConnectionWizardViewModel ViewModel { get; }

    public ConnectionWizardPage()
    {
        Log.Information("ConnectionWizardPage Constructor Started");
        ViewModel = ((App)Application.Current).Services.GetRequiredService<ConnectionWizardViewModel>();
        DataContext = this;
        InitializeComponent();
    }

    public void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && sender is PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }
}