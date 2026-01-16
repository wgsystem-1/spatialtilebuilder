namespace SpatialTileBuilder.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Core.Interfaces;

public partial class LayerSelectionViewModel : ObservableObject
{
    private readonly IPostGISConnectionService _connectionService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<SelectableSpatialTable> Tables { get; } = new();



    public event EventHandler<List<SelectableSpatialTable>>? NextRequested;

    private readonly SpatialTileBuilder.App.Services.ProjectStateService _stateService;

    public LayerSelectionViewModel(IPostGISConnectionService connectionService, SpatialTileBuilder.App.Services.ProjectStateService stateService)
    {
        _connectionService = connectionService;
        _stateService = stateService;
    }

    [RelayCommand]
    public async Task LoadTablesAsync()
    {
        IsLoading = true;
        Tables.Clear();
        StatusMessage = "Loading spatial tables...";

        try
        {
            var tables = await _connectionService.GetSpatialTablesAsync();
            
            foreach (var table in tables)
            {
                var selectable = new SelectableSpatialTable(table);
                // Reselect if previously selected
                if (_stateService.SelectedLayers.Any(l => l.Table.Table == table.Table && l.Table.Schema == table.Schema))
                {
                    selectable.IsSelected = true;
                }
                Tables.Add(selectable);
            }

            if (Tables.Count == 0)
            {
                // Diagnostic Logic
                try 
                {
                    using var conn = _connectionService.CreateConnection();
                    
                    // 1. Get current connected DB name
                    string currentDb = await Dapper.SqlMapper.ExecuteScalarAsync<string>(conn, "SELECT current_database()") ?? "unknown";
                    
                    // 2. Check table count in current DB
                    var sqlCount = @"
                        SELECT count(*) 
                        FROM information_schema.tables 
                        WHERE table_schema NOT IN ('information_schema', 'pg_catalog') 
                          AND table_schema NOT LIKE 'pg_%'";
                    var tableCount = await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, sqlCount);

                    // 3. Get LIST of Visible Schemas
                    var schemas = await Dapper.SqlMapper.QueryAsync<string>(conn, 
                        "SELECT nspname FROM pg_namespace " +
                        "WHERE nspname NOT LIKE 'pg_%' AND nspname != 'information_schema'");
                    var schemaListStr = string.Join(", ", schemas);

                    string currentUser = await Dapper.SqlMapper.ExecuteScalarAsync<string>(conn, "SELECT current_user") ?? "unknown";

                    StatusMessage = $"User: '{currentUser}' on DB: '{currentDb}'. Visible Schemas: [{schemaListStr}]. Total Tables: {tableCount}.";
                }
                catch (Exception dbEx)
                {
                    StatusMessage = $"Diagnostic failed: {dbEx.Message}";
                }
            }
            else
            {
                StatusMessage = $"Found {Tables.Count} spatial tables.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading tables: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NavigateNext()
    {
        var selected = Tables.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Please select at least one table.";
            return;
        }

        // Save state
        _stateService.SelectedLayers = selected;

        NextRequested?.Invoke(this, selected);
    }

    [RelayCommand]
    private async Task CalculateExtentAsync(SelectableSpatialTable? item)
    {
        if (item == null) return;

        IsLoading = true;
        try
        {
            var bbox = await _connectionService.GetLayerExtentAsync(item.Table.Schema, item.Table.Table);
            StatusMessage = $"Extent for {item.Table.Table}: [{bbox.MinX:F4}, {bbox.MinY:F4}, {bbox.MaxX:F4}, {bbox.MaxY:F4}]";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error getting extent: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
