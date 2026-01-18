namespace SpatialTileBuilder.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialTileBuilder.App.Services;
using SpatialTileBuilder.App.ViewModels.Dialogs;
using SpatialTileBuilder.App.Views.Dialogs;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.Infrastructure.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

public partial class SourceBrowserViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly DataSourceFactory _dataSourceFactory;

    [ObservableProperty]
    private ObservableCollection<DataSourceItemViewModel> _sources = new();

    [ObservableProperty]
    private object? _selectedItem;

    public SourceBrowserViewModel(ProjectService projectService, DataSourceFactory dataSourceFactory)
    {
        _projectService = projectService;
        _dataSourceFactory = dataSourceFactory;

        // Listen for project changes if needed, or simply refresh.
        // For MVP, we'll reload when the command triggers or explicit Refresh.
        _projectService.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(ProjectService.CurrentProject)) RefreshSources();
        };

        RefreshSources();
    }

    private void RefreshSources()
    {
        Sources.Clear();
        if (_projectService.CurrentProject == null) return;

        foreach (var dsConfig in _projectService.CurrentProject.DataSources)
        {
            var vm = new DataSourceItemViewModel(dsConfig, _dataSourceFactory);
            Sources.Add(vm);
        }
    }

    [RelayCommand]
    private void AddSource()
    {
        var vm = new AddSourceDialogViewModel();
        var dialog = new AddSourceDialog { DataContext = vm };
        vm.CloseAction = () => dialog.Close();

        dialog.ShowDialog();

        if (vm.ResultConfig != null)
        {
            _projectService.AddDataSource(vm.ResultConfig);
            // Refresh auto-triggers via PropertyChanged
        }
    }

    [RelayCommand]
    private void RemoveSource()
    {
        if (SelectedItem is DataSourceItemViewModel dsVm)
        {
            _projectService.RemoveDataSource(dsVm.Config.Id);
        }
    }

    [RelayCommand]
    private void AddLayerToMap()
    {
        if (SelectedItem is TableItemViewModel tableVm)
        {
            var config = new LayerConfig(
                Id: System.Guid.NewGuid().ToString(),
                Name: tableVm.TableName,
                DataSourceId: tableVm.OwnerSourceId,
                SourceName: tableVm.TableName,
                IsVisible: true,
                Opacity: 1.0,
                FillColor: "#FF0000",
                IsFillVisible: true,
                StrokeColor: "#000000",
                StrokeWidth: 1.0,
                StrokeDashArray: "",
                LabelColumn: null,
                LabelSize: 12,
                LabelColor: "#000000",
                LabelHaloRadius: 1,
                FontName: "Arial",
                PointColor: "#FF0000",
                PointSize: 5.0
            );

            _projectService.AddLayer(config);
        }
    }
}

public partial class DataSourceItemViewModel : ObservableObject
{
    public DataSourceConfig Config { get; }
    private readonly DataSourceFactory _factory;

    [ObservableProperty]
    private ObservableCollection<TableItemViewModel> _tables = new();

    [ObservableProperty]
    private bool _isExpanded;

    public string Name => Config.Name;
    public string IconKind => Config.Type == Core.DTOs.DataSourceType.PostGIS ? "Database" : "File";

    public DataSourceItemViewModel(DataSourceConfig config, DataSourceFactory factory)
    {
        Config = config;
        _factory = factory;
        // Lazy load tables when expanded? Or just load now?
        // Let's load on expand for better UX or async loading.
        // For simplicity, let's just trigger loading.
        LoadTablesAsync(); 
    }

    public async void LoadTablesAsync()
    {
        try
        {
            var dsService = _factory.Create(Config);
            if (await dsService.TestConnectionAsync())
            {
                var tables = await dsService.GetTablesAsync();
                Tables.Clear();
                foreach (var t in tables)
                {
                    Tables.Add(new TableItemViewModel(t.Table, Config.Id));
                }
            }
        }
        catch (System.Exception)
        {
            // Handle error (show red icon?)
        }
    }
}

public partial class TableItemViewModel : ObservableObject
{
    public string TableName { get; }
    public string OwnerSourceId { get; }

    public TableItemViewModel(string tableName, string ownerSourceId)
    {
        TableName = tableName;
        OwnerSourceId = ownerSourceId;
    }
}
