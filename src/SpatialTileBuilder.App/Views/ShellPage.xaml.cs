using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace SpatialTileBuilder.App.Views;

public partial class ShellPage : Page
{
    public ShellPage()
    {
        InitializeComponent();
        DataContext = this;
        ContentFrame.Navigated += ContentFrame_Navigated;
        
        // Navigate to the first page by default
        Loaded += (s, e) =>
        {
            if (NavListBox.Items.Count > 0)
            {
                NavListBox.SelectedIndex = 0;
            }
        };
    }

    private void ContentFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        var pageName = e.Content?.GetType().Name;
        var item = NavListBox.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag is string tag && tag == pageName);

        if (item != null)
        {
            NavListBox.SelectedItem = item;
        }
    }

    private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is ListBoxItem selectedItem && 
            selectedItem.Tag is string pageName)
        {
            try
            {
                Page? page = CreatePageInstance(pageName);
                
                if (page != null)
                {
                    ContentFrame.Navigate(page);
                }
            }
            catch (Exception ex)
            {
                // 상세 에러 내용을 찾기 위해 InnerException을 파고듭니다.
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"네비게이션 실패 (페이지: {pageName})");
                sb.AppendLine($"종류: {ex.GetType().Name}");
                sb.AppendLine($"메시지: {ex.Message}");
                
                var inner = ex.InnerException;
                int depth = 1;
                while (inner != null)
                {
                    sb.AppendLine(new string('-', 20));
                    sb.AppendLine($"내부 원인 [{depth}]: {inner.GetType().Name}");
                    sb.AppendLine($"메시지: {inner.Message}");
                    if (inner is System.Windows.Markup.XamlParseException xamlEx)
                    {
                        sb.AppendLine($"XAML 라인: {xamlEx.LineNumber}, 위치: {xamlEx.LinePosition}");
                    }
                    inner = inner.InnerException;
                    depth++;
                }

                string errorMsg = sb.ToString();
                System.Diagnostics.Debug.WriteLine(errorMsg);
                Log.Error(ex, "Navigation Failed Details");
                
                MessageBox.Show(errorMsg, "치명적 오류 상세", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private Page? CreatePageInstance(string pageName)
    {
        return pageName switch
        {
            "ConnectionWizardPage" => new ConnectionWizardPage(),
            "LayerSelectionPage" => new LayerSelectionPage(),
            "StylePreviewPage" => new StylePreviewPage(),
            "RegionSelectionPage" => new RegionSelectionPage(),
            "GenerationMonitorPage" => new GenerationMonitorPage(),
            "SettingsPage" => new SettingsPage(),
            _ => null
        };
    }
}