using System.Windows.Controls;
using System.Windows;
using SpatialTileBuilder.App.ViewModels;

namespace SpatialTileBuilder.App.Views.Components;

public partial class SourceBrowserView : UserControl
{
    public SourceBrowserView()
    {
        InitializeComponent();
        
        // Handle TreeView SelectedItem binding workaround
        var treeView = this.Content as Grid;
        foreach(var child in treeView.Children)
        {
            if (child is TreeView tv)
            {
                tv.SelectedItemChanged += (s, e) => 
                {
                    if (DataContext is SourceBrowserViewModel vm)
                    {
                        vm.SelectedItem = e.NewValue;
                    }
                };
            }
        }
    }
}
