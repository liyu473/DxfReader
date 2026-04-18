using DXFReaderDemo.Views;
using LyuExtensions.Aspects;
using System.Windows;

namespace DXFReaderDemo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
[Singleton]
public partial class MainWindow : Window
{
    [Inject]
    private readonly DxfReaderView _dxfReaderView;

    public MainWindow()
    {
        InitializeComponent();
        ContentArea.Content = _dxfReaderView;
    }
}
