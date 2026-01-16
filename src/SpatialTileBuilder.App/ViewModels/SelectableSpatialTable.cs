namespace SpatialTileBuilder.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using SpatialTileBuilder.Core.DTOs;

public partial class SelectableSpatialTable : ObservableObject
{
    public SpatialTable Table { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectableSpatialTable(SpatialTable table)
    {
        Table = table;
    }
}
