using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TaskbarTails;

// PixelLab sprites only. The preview is large; desktop pets use one third of the v0.2 size.
public sealed class PetVisual : FrameworkElement
{
    public string Species = "cat";
    public string PetName = "모찌";
    public bool Sleeping, FrontView, Walking, FaceLeft;
    public double Phase, Jump, ActionTime, Sway, BallX, BallY;
    public bool Parachute, BallVisible;
    public string ActionKey = "";
    public string Bubble = "";
    public bool ShowName { get; set; } = true;
    static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    static readonly Brush Ink = B("#49403C"), Cream = B("#FFFDF8"), Teal = B("#367969");
    static readonly Typeface Regular = new(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Typeface Bold = new(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    FormattedText Make(string value, double size, Brush color, bool bold, double maxWidth = 0)
    {
        var f = new FormattedText(value, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, bold ? Bold : Regular, size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (maxWidth > 0) { f.MaxTextWidth = maxWidth; f.Trimming = TextTrimming.None; }
        return f;
    }
    void Text(DrawingContext d, string value, double size, Brush color, double x, double y, bool bold = false)
    {
        var f = Make(value, size, color, bold);
        d.DrawText(f, new Point(x - f.Width / 2, y));
    }
    // Speech bubble grows upward from the pet and wraps long text (up to 80 characters) inside the 160px overlay.
    void DrawBubble(DrawingContext d, double bottom)
    {
        var f = Make(Bubble, 10, Ink, true, 128);
        double width = Math.Min(150, Math.Ceiling(f.Width) + 16), height = Math.Ceiling(f.Height) + 10;
        var box = new Rect(80 - width / 2, Math.Max(2, bottom - height), width, height);
        d.DrawRoundedRectangle(Cream, new Pen(B("#DDD9CD"), 1), box, 10, 10);
        var tail = new StreamGeometry();
        using (var g = tail.Open()) { g.BeginFigure(new Point(76, box.Bottom - 1), true, true); g.LineTo(new Point(80, box.Bottom + 5), true, false); g.LineTo(new Point(84, box.Bottom - 1), true, false); }
        tail.Freeze(); d.DrawGeometry(Cream, null, tail);
        d.DrawText(f, new Point(box.Left + 8, box.Top + 5));
    }
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        var sprites = SpriteLibrary.Get(Species);
        if (sprites == null) return;
        double scale = Math.Min(ActualWidth / 160, ActualHeight / 180);
        d.PushTransform(new TranslateTransform((ActualWidth - 160 * scale) / 2, (ActualHeight - 180 * scale) / 2));
        d.PushTransform(new ScaleTransform(scale, scale));
        double petScale = FrontView ? 1 : 1.0 / 3;
        d.DrawEllipse(B("#19000000"), null, new Point(80, 174), Math.Max(5, 25 * petScale - Jump / 12), FrontView ? 4 : 2);
        if (ShowName && !Parachute) Text(d, PetName, 10, Ink, 80, 119);
        if (Bubble.Length > 0) DrawBubble(d, FrontView ? 40 : 112);
        else if (Sleeping) Text(d, "z z Z", 12, Teal, 108, FrontView ? 38 : 102);
        var (frame, flip) = sprites.Frame(FaceLeft, Walking && !Sleeping, Phase, FrontView, ActionKey, ActionTime);
        double zoom = Math.Min(3, Math.Min(126 / sprites.Bounds.Width, 113 / sprites.Bounds.Height)) * petScale;
        double px = 80 - (sprites.Bounds.Left + sprites.Bounds.Width / 2) * zoom;
        double py = 174 - sprites.Bounds.Bottom * zoom - Jump * petScale;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        if (Parachute) d.PushTransform(new RotateTransform(Sway, 80, 138));
        if (flip) d.PushTransform(new ScaleTransform(-1, 1, 80, 0));
        d.DrawImage(frame, new Rect(Math.Round(px), Math.Round(py), frame.PixelWidth * zoom, frame.PixelHeight * zoom));
        if (flip) d.Pop();
        if (Parachute)
        {
            // Small code-native pixel prop; all characters themselves come from PixelLab.
            var rope = new Pen(B("#766D61"), 1);
            d.DrawLine(rope, new Point(55, 117), new Point(76, 142));
            d.DrawLine(rope, new Point(105, 117), new Point(84, 142));
            d.DrawLine(rope, new Point(80, 114), new Point(80, 142));
            string[] canopy = { "000001111100000", "000111111111000", "001111111111100", "011111111111110", "111111111111111", "111111111111111" };
            for (int row = 0; row < canopy.Length; row++) for (int col = 0; col < canopy[row].Length; col++) if (canopy[row][col] == '1')
                d.DrawRectangle(B(col < 5 ? "#8FC7AD" : col < 10 ? "#FFF1CB" : "#EAA69E"), null, new Rect(54 + col * 3.5, 96 + row * 3.5, 3.5, 3.5));
            d.Pop();
        }
        d.Pop(); d.Pop();
    }
}
