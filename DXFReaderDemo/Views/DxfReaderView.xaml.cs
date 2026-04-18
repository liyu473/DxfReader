using DXFReaderDemo.ViewModels;
using LyuExtensions.Aspects;
using System.Windows;
using System.Windows.Controls;

namespace DXFReaderDemo.Views;

[Singleton]
public partial class DxfReaderView : UserControl
{
    [Inject]
    private readonly DxfReaderViewModel _vm;

    public DxfReaderView()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsDrawer.IsOpen = !SettingsDrawer.IsOpen;
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        PreviewViewer.ResetView();
    }
}
