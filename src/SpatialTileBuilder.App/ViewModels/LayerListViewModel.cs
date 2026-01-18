namespace SpatialTileBuilder.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.App.Services;
using SpatialTileBuilder.Core.DTOs;
using System.Collections.ObjectModel;
using System.Linq;

public partial class LayerListViewModel : ObservableObject
{
    private readonly ProjectService _projectService;

    [ObservableProperty]
    private ObservableCollection<LayerItemViewModel> _layers = new();

    [ObservableProperty]
    private LayerItemViewModel? _selectedLayer;

    public LayerListViewModel(ProjectService projectService)
    {
        _projectService = projectService;
        _projectService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProjectService.CurrentProject)) RefreshLayers();
        };
        RefreshLayers();
    }

    private void RefreshLayers()
    {
        Layers.Clear();
        if (_projectService.CurrentProject == null) return;

        foreach (var layerConfig in _projectService.CurrentProject.Layers)
        {
            Layers.Add(new LayerItemViewModel(layerConfig));
        }
    }

    [RelayCommand]
    private void MoveLayerUp()
    {
        if (SelectedLayer == null) return;
        var index = Layers.IndexOf(SelectedLayer);
        if (index > 0)
        {
            Layers.Move(index, index - 1);
            UpdateProjectService();
        }
    }

    [RelayCommand]
    private void MoveLayerDown()
    {
        if (SelectedLayer == null) return;
        var index = Layers.IndexOf(SelectedLayer);
        if (index < Layers.Count - 1)
        {
            Layers.Move(index, index + 1);
            UpdateProjectService();
        }
    }
    
    [RelayCommand]
    private void RemoveLayer()
    {
        if (SelectedLayer == null) return;
        Layers.Remove(SelectedLayer);
        UpdateProjectService();
    }

    public void UpdateProjectService()
    {
        // Sync back to Project Service
        // Map ItemViewModels back to Configs
        var newLayers = Layers.Select(vm => vm.Config).ToList();
        _projectService.UpdateLayers(newLayers);
    }
}

public partial class LayerItemViewModel : ObservableObject
{
    // Wrap Config. Changes here should update Config or notify parent.
    // For MVP, we update properties directly.
    
    [ObservableProperty] private LayerConfig _config;

    public string Name => Config.Name;

    // Proxy properties for binding
    public bool IsVisible
    {
        get => Config.IsVisible;
        set
        {
            if (Config.IsVisible != value)
            {
                Config = Config with { IsVisible = value };
                OnPropertyChanged();
            }
        }
    }

    public LayerItemViewModel(LayerConfig config)
    {
        _config = config;
    }
}
