using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.App.Services;
using SpatialTileBuilder.Core.DTOs;
using SpatialTileBuilder.App.ViewModels;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SpatialTileBuilder.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public async Task TestProjectSaveAndLoad()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ProjectService>>();
            var service = new ProjectService(loggerMock.Object);
            string tempFile = Path.GetTempFileName();

            // Act: Create & Modify
            service.CreateNewProject();
            service.CurrentProject = service.CurrentProject with { ProjectName = "Test Project" };
            
            var layer = new LayerConfig(
                Id: "layer1",
                Name: "Test Layer",
                DataSourceId: "ds1",
                SourceName: "table_name",
                IsVisible: true,
                Opacity: 1.0,
                FillColor: "#FFFFFF",
                IsFillVisible: true,
                StrokeColor: "#000000",
                StrokeWidth: 1.0,
                StrokeDashArray: "Solid",
                LabelColumn: "name",
                LabelSize: 12,
                LabelColor: "#000000",
                LabelHaloRadius: 0,
                FontName: "Arial",
                PointColor: "#FF0000",
                PointSize: 5,
                Rules: new List<StyleRule> 
                {
                    new StyleRule("Rule1", null, "#ABCDEF", "#123456", 2.0, true)
                }
            );
            service.AddLayer(layer);

            // Act: Save
            await service.SaveProjectAsync(tempFile);

            // Act: Load
            var loadedService = new ProjectService(loggerMock.Object);
            await loadedService.LoadProjectAsync(tempFile);

            // Assert
            Assert.Equal("Test Project", loadedService.CurrentProject.ProjectName);
            Assert.Single(loadedService.CurrentProject.Layers);
            Assert.Equal("layer1", loadedService.CurrentProject.Layers[0].Id);
            
            var rules = loadedService.CurrentProject.Layers[0].Rules;
            Assert.NotNull(rules);
            Assert.Single(rules);
            Assert.Equal("Rule1", rules[0].Name);

            // Cleanup
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
