using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SpatialTileBuilder.App.ViewModels;

namespace SpatialTileBuilder.App.Views;

public partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<LoginViewModel>();
        DataContext = this;
        InitializeComponent();
        
        ViewModel.LoginSuccess += ViewModel_LoginSuccess;
    }

    private void ViewModel_LoginSuccess(object? sender, EventArgs e)
    {
        // WPF navigation
        Dispatcher.Invoke(() =>
        {
            try
            {
                NavigationService?.Navigate(new ShellPage());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation failed: {ex.Message}");
            }
        });
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && sender is PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }
}