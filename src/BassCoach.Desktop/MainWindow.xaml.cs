using System.Windows;
using System.Windows.Media;

namespace BassCoach.Desktop;

public partial class MainWindow : Window
{
    private const string MaximizeGeometry = "M 0.5,0.5 L 9.5,0.5 L 9.5,9.5 L 0.5,9.5 Z";
    private const string RestoreGeometry = "M 2.5,0.5 L 9.5,0.5 L 9.5,7.5 M 0.5,2.5 L 7.5,2.5 L 7.5,9.5 L 0.5,9.5 Z";

    public MainWindow()
    {
        InitializeComponent();
        FitToWorkArea();
        StateChanged += (_, _) => UpdateWindowState();
        Loaded += (_, _) => UpdateWindowState();
    }

    /// <summary>Keeps the window usable on small screens or high display scaling.</summary>
    private void FitToWorkArea()
    {
        var work = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, work.Width - 24);
        MinHeight = Math.Min(MinHeight, work.Height - 24);
        Width = Math.Min(Width, work.Width - 24);
        Height = Math.Min(Height, work.Height - 24);
    }

    private void UpdateWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;

        // A maximized WindowChrome window is inflated past the work area by the resize border,
        // so pull the content back in to keep the edges visible.
        var border = SystemParameters.WindowResizeBorderThickness;
        RootBorder.Margin = maximized
            ? new Thickness(border.Left + 1, border.Top + 1, border.Right + 1, border.Bottom + 1)
            : new Thickness(0);

        MaximizeGlyph.Data = Geometry.Parse(maximized ? RestoreGeometry : MaximizeGeometry);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
