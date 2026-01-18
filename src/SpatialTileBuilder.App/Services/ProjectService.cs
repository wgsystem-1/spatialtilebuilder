namespace SpatialTileBuilder.App.Services;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.DTOs;

public partial class ProjectService : ObservableObject
{
    private readonly ILogger<ProjectService> _logger;

    [ObservableProperty]
    private ProjectConfiguration _currentProject;

    [ObservableProperty]
    private string _currentProjectPath;

    public ProjectService(ILogger<ProjectService> logger)
    {
        _logger = logger;
        CreateNewProject();
    }

    public void CreateNewProject()
    {
        CurrentProject = new ProjectConfiguration(
            ProjectName: "Untitled Project",
            DataSources: new(),
            Layers: new(),
            CenterX: 127.0,
            CenterY: 37.5,
            Zoom: 7
        );
        CurrentProjectPath = string.Empty;
    }

    public async Task SaveProjectAsync(string path)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentProject, options);
            await File.WriteAllTextAsync(path, json);
            CurrentProjectPath = path;
            _logger.LogInformation("Project saved to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project.");
            throw;
        }
    }

    public async Task LoadProjectAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Project file not found.", path);

            var json = await File.ReadAllTextAsync(path);
            var project = JsonSerializer.Deserialize<ProjectConfiguration>(json);
            
            if (project != null)
            {
                CurrentProject = project;
                CurrentProjectPath = path;
                _logger.LogInformation("Project loaded from {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project.");
            throw;
        }
    }
    public void AddDataSource(DataSourceConfig source)
    {
        var sources = new System.Collections.Generic.List<DataSourceConfig>(CurrentProject.DataSources);
        sources.Add(source);
        
        CurrentProject = CurrentProject with { DataSources = sources };
        _logger.LogInformation("Added DataSource {Name}", source.Name);
    }

    public void RemoveDataSource(string sourceId)
    {
        var sources = new System.Collections.Generic.List<DataSourceConfig>(CurrentProject.DataSources);
        sources.RemoveAll(s => s.Id == sourceId);
        
        CurrentProject = CurrentProject with { DataSources = sources };
        _logger.LogInformation("Removed DataSource {Id}", sourceId);
    }

    public void AddLayer(LayerConfig layer)
    {
        var layers = new System.Collections.Generic.List<LayerConfig>(CurrentProject.Layers);
        layers.Add(layer);

        CurrentProject = CurrentProject with { Layers = layers };
        _logger.LogInformation("Added Layer {Name}", layer.Name);
    }

    public void UpdateLayers(System.Collections.Generic.List<LayerConfig> layers)
    {
        CurrentProject = CurrentProject with { Layers = layers };
    }
}
