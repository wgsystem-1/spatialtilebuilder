using System.Windows;
using System.Windows.Controls;
using SpatialTileBuilder.App.ViewModels.Dialogs;

namespace SpatialTileBuilder.App.Views.Dialogs;

public partial class AddSourceDialog : Window
{
    public AddSourceDialog()
    {
        InitializeComponent();
        // Manual Password binding or handling if needed, simplified for MVP
        // To make it work with ViewModel, we'd need to push password on change or verify via Box.
        // For MVP, letting ViewModel have "Password" property is fine, but PasswordBox doesn't bind directly.
        // Using a simple event handler to update VM:
        PasswordBox.PasswordChanged += (s, e) => 
        { 
            if (DataContext is AddSourceDialogViewModel vm) vm.Password = PasswordBox.Password; 
        };
    }
}
