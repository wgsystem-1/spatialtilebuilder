namespace SpatialTileBuilder.App.ViewModels;

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.Interfaces;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    
    // Event to notify view of successful login
    public event EventHandler? LoginSuccess;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isLoading;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    private bool CanLogin => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        
        try
        {
            await Task.Delay(500); // UI feedback
            var result = await _authService.LoginAsync(Username, Password);
            
            if (result.IsSuccess)
            {
                LoginSuccess?.Invoke(this, EventArgs.Empty);
            }
            else 
            {
                ErrorMessage = result.ErrorMessage ?? "Login failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
