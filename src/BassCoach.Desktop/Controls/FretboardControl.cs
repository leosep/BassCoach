using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BassCoach.Core.Practice;
using BassCoach.Core.Theory;

namespace BassCoach.Desktop.Controls;

/// <summary>Draws a bass neck (frets 0-12) with the chord tones of the current chord.</summary>
public sealed class FretboardControl : FrameworkElement
{
    public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(
        nameof(Markers), typeof(IReadOnlyList<FretMarker>), typeof(FretboardControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StringCountProperty = DependencyProperty.Register(
        nameof(StringCount), typeof(int), typeof(FretboardControl),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

    private const int Frets = 12;
    private static readonly Brush Wood = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x22, 0x1C)));
    private static readonly Brush FretWire = Freeze(new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)));
    private static readonly Brush Inlay = Freeze(new SolidColorBrush(Color.FromRgb(0x4B, 0x41, 0x38)));
    private static readonly Brush StringBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)));
    private static readonly Brush RootBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x89, 0x0C)));
    private static readonly Brush ToneBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E)));

    public IReadOnlyList<FretMarker>? Markers
    {
        get => (IReadOnlyList<FretMarker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public int StringCount
    {
        get => (int)GetValue(StringCountProperty);
        set => SetValue(StringCountProperty, value);
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth, height = ActualHeight;
        if (width < 100 || height < 40) return;

        const double nutX = 36;
        double top = 14, bottom = height - 14;
        double fretWidth = (width - nutX - 10) / Frets;
        int strings = Math.Max(2, StringCount);
        double stringGap = (bottom - top) / (strings - 1);

        dc.DrawRoundedRectangle(Wood, null, new Rect(nutX, top - 8, width - nutX - 4, bottom - top + 16), 6, 6);

        var fretPen = new Pen(FretWire, 1.5);
        for (int fret = 1; fret <= Frets; fret++)
            dc.DrawLine(fretPen, new Point(nutX + fret * fretWidth, top - 8), new Point(nutX + fret * fretWidth, bottom + 8));
        dc.DrawLine(new Pen(StringBrush, 4), new Point(nutX, top - 8), new Point(nutX, bottom + 8));

        double middle = (top + bottom) / 2;
        foreach (int fret in new[] { 3, 5, 7, 9 })
            dc.DrawEllipse(Inlay, null, new Point(nutX + (fret - 0.5) * fretWidth, middle), 6, 6);
        dc.DrawEllipse(Inlay, null, new Point(nutX + 11.5 * fretWidth, middle - stringGap * 0.8), 6, 6);
        dc.DrawEllipse(Inlay, null, new Point(nutX + 11.5 * fretWidth, middle + stringGap * 0.8), 6, 6);

        for (int s = 0; s < strings; s++)
        {
            double y = bottom - s * stringGap; // string 0 (lowest) at the bottom, like looking down at the bass
            dc.DrawLine(new Pen(StringBrush, 1.2 + (strings - 1 - s) * 0.6), new Point(nutX - 6, y), new Point(width - 8, y));
        }

        if (Markers is null) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var marker in Markers)
        {
            if (marker.String >= strings) continue;
            double x = marker.Fret == 0 ? nutX - 18 : nutX + (marker.Fret - 0.5) * fretWidth;
            double y = bottom - marker.String * stringGap;
            var center = new Point(x, y);
            dc.DrawEllipse(marker.Role == NoteRole.Root ? RootBrush : ToneBrush, null, center, 11, 11);
            string label = marker.Role switch { NoteRole.Root => "R", NoteRole.Third => "3", NoteRole.Fifth => "5", _ => "7" };
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"), 12, Brushes.White, dpi);
            dc.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
        }
    }
}
