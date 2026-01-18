namespace SpatialTileBuilder.App.ViewModels.Dialogs;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Enums;
using System;
using System.Windows;

public partial class AddSourceDialogViewModel : ObservableObject
{
    // Mode Selection
    [ObservableProperty] private int _selectedTabIndex; // 0=PostGIS, 1=File

    // PostGIS Inputs
    [ObservableProperty] private string _host = "localhost";
    [ObservableProperty] private int _port = 5432;
    [ObservableProperty] private string _database = "";
    [ObservableProperty] private string _username = "postgres";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _sslMode = "Disable";
    [ObservableProperty] private string _sourceName = "";

    // File Inputs
    [ObservableProperty] private string _filePath = "";

    public DataSourceConfig? ResultConfig { get; private set; }

    public Action? CloseAction { get; set; }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Spatial Files (*.shp;*.geojson)|*.shp;*.geojson|All Files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
            if (string.IsNullOrEmpty(SourceName))
            {
                SourceName = System.IO.Path.GetFileNameWithoutExtension(FilePath);
            }
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ResultConfig = null;
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Add()
    {
        if (SelectedTabIndex == 0) // PostGIS
        {
            if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Database) || string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Please fill in required fields.");
                return;
            }

            var builder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = Host,
                Port = Port,
                Database = Database,
                Username = Username,
                Password = Password,
                SslMode = ParseSslMode(SslMode)
            };

            var name = string.IsNullOrWhiteSpace(SourceName) ? $"{Database}@{Host}" : SourceName;

            ResultConfig = new DataSourceConfig(
                Id: Guid.NewGuid().ToString(),
                Name: name,
                Type: DataSourceType.PostGIS,
                ConnectionString: builder.ConnectionString,
                Provider: "Npgsql"
            );
        }
        else // File
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            
            var name = string.IsNullOrWhiteSpace(SourceName) ? System.IO.Path.GetFileNameWithoutExtension(FilePath) : SourceName;
            
            var type = FilePath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase) ? DataSourceType.Shapefile : DataSourceType.GeoJson;

            ResultConfig = new DataSourceConfig(
                Id: Guid.NewGuid().ToString(),
                Name: name,
                Type: type,
                ConnectionString: FilePath,
                Provider: "File"
            );
        }

        CloseAction?.Invoke();
    }

    private Npgsql.SslMode ParseSslMode(string mode)
    {
         return mode switch { "Require" => Npgsql.SslMode.Require, _ => Npgsql.SslMode.Disable };
    }
}
