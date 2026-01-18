namespace SpatialTileBuilder.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System;

public partial class MainWorkspaceViewModel : ObservableObject
{
    // ViewModels for child panes
    public SourceBrowserViewModel SourceBrowser { get; }
    public MapCanvasViewModel MapCanvas { get; }
    public LayerListViewModel LayerList { get; }
    public ExportViewModel ExportVM { get; }

    // Properties Pane
    public LayerPropertiesViewModel LayerProperties { get; } = new();

    private readonly Infrastructure.Data.DataSourceFactory _dataSourceFactory;
    private readonly Services.ProjectService _projectService;
    private readonly System.Collections.Generic.Dictionary<string, Core.Interfaces.IDataSourceService> _activeDataSources = new();
    


    public MainWorkspaceViewModel(
        SourceBrowserViewModel sourceBrowser, 
        MapCanvasViewModel mapCanvas, 
        LayerListViewModel layerList,
        ExportViewModel exportVM,
        Infrastructure.Data.DataSourceFactory dataSourceFactory,
        Services.ProjectService projectService)
    {
        SourceBrowser = sourceBrowser;
        MapCanvas = mapCanvas;
        LayerList = layerList;
        ExportVM = exportVM;
        _dataSourceFactory = dataSourceFactory;
        
        ExportVM.Initialize(new Core.DTOs.BoundingBox(-180, -85, 180, 85)); // Default init

        _projectService = projectService;

        LayerList.PropertyChanged += LayerList_PropertyChanged;
        LayerProperties.PropertyChanged += LayerProperties_PropertyChanged;
    }

    [ObservableProperty]
    private bool _isExportVisible;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenExport()
    {
        IsExportVisible = true;
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CloseExport()
    {
        IsExportVisible = false;
    }

    private void LayerList_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayerListViewModel.SelectedLayer))
        {
            var selected = LayerList.SelectedLayer;
            if (selected != null)
            {
                var ds = GetOrServiceDataSource(selected.Config.DataSourceId);
                LayerProperties.DataSourceService = ds; // We need to expose this setter in VM
                LayerProperties.LoadLayer(selected.Config);
            }
            else
            {
                LayerProperties.LoadLayer(null!); // Clear or handle null
            }
        }
    }

    private void LayerProperties_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // If properties change, we might need to update the layer list or map
        // For now, LayerProperties updates its internal state. 
        // We should probably save changes back to LayerList selected item when things change.
        // Actually LayerPropertiesViewModel has GetUpdatedLayer().
        
        // Simple syncing for now:
        if (LayerList.SelectedLayer != null && e.PropertyName != nameof(LayerPropertiesViewModel.GetUpdatedLayer)) 
        {
             // Update the config in LayerItemViewModel
             var updated = LayerProperties.GetUpdatedLayer();
             if (updated != null)
             {
                 LayerList.SelectedLayer.Config = updated;
                 LayerList.UpdateProjectService();
                 // Trigger map redraw?
                 MapCanvas.Refresh(); 
             }
        }
    }

    private Core.Interfaces.IDataSourceService? GetOrServiceDataSource(string id)
    {
        if (_activeDataSources.TryGetValue(id, out var ds)) return ds;
        
        var config = _projectService.CurrentProject.DataSources.Find(d => d.Id == id);
        if (config == null) return null;

        var newDs = _dataSourceFactory.Create(config);
        _activeDataSources[id] = newDs;
        return newDs;
    }
}
