using Avalonia.Controls;

namespace Trail.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // enable overlay
        var top = GetTopLevel(this)!;
        top.RendererDiagnostics.DebugOverlays = Avalonia.Rendering.RendererDebugOverlays.Fps;
    }
}