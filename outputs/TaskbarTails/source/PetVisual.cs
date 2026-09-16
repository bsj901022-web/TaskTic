using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TaskbarTails;

// Original vector artwork, rendered locally: no downloaded assets or image dependencies.
public sealed class PetVisual : FrameworkElement
{
    public string Species = "cat";
    public string PetName = "모찌";
    public string Coat = "#FFF2DE";
    public bool Sleeping;
    public bool Walking;
    public bool FaceLeft;
    public double Phase;
    public double Jump;
    public string Bubble = "";
    public bool ShowName { get; set; } = true;
    static Brush B(string hex) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze(); return brush; }
    static readonly Brush Ink = B("#49403C"), Pink = B("#EDA79E"), Cream = B("#FFFDF8"), Teal = B("#367969");
    static readonly Pen Outline = new(Ink, 2.6) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
    void Text(DrawingContext d, string value, double size, Brush color, double x, double y, bool bold = false)
    {
        var f = new FormattedText(value, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        d.DrawText(f, new Point(x - f.Width / 2, y));
    }
    static void Shape(DrawingContext d, string path, Brush fill, Pen? stroke = null) => d.DrawGeometry(fill, stroke, Geometry.Parse(path));
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        double scale = Math.Min(ActualWidth / 160, ActualHeight / 180);
        d.PushTransform(new TranslateTransform((ActualWidth - 160 * scale) / 2, (ActualHeight - 180 * scale) / 2));
        d.PushTransform(new ScaleTransform(scale, scale));
        d.DrawEllipse(B("#19000000"), null, new Point(80, 174), Math.Max(15, 34 - Jump / 4), 4);
        if (ShowName) Text(d, PetName, 11, Ink, 80, 37);
        if (Bubble.Length > 0)
        {
            d.DrawRoundedRectangle(Cream, new Pen(B("#DDD9CD"), 1), new Rect(8, 2, 144, 29), 14, 14);
            Text(d, Bubble, 11, Ink, 80, 8, true);
        }
        else if (Sleeping) Text(d, "z  z  Z", 16, Teal, 111, 31);
        double bob = Walking && !Sleeping ? Math.Sin(Phase * 10) * 2 : Math.Sin(Phase * 2) * 1;
        d.PushTransform(new TranslateTransform(0, -Jump + bob));
        if (FaceLeft) d.PushTransform(new ScaleTransform(-1, 1, 80, 0));
        Brush fur = B(Coat);
        if (Species == "cat")
        {
            var tail = new Pen(fur, 12) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var border = new Pen(Ink, 17) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var path = Geometry.Parse($"M 108,147 Q 146,152 137,{126 + Math.Sin(Phase * 3) * 5}");
            d.DrawGeometry(null, border, path); d.DrawGeometry(null, tail, path);
        }
        else d.DrawEllipse(Cream, Outline, new Point(115, 148), 12, 12);
        double step = Walking && !Sleeping ? Math.Sin(Phase * 10) * 4 : 0;
        d.DrawEllipse(fur, Outline, new Point(64, 168 - Math.Max(0, step)), 13, 7);
        d.DrawEllipse(fur, Outline, new Point(97, 168 - Math.Max(0, -step)), 13, 7);
        d.DrawEllipse(fur, Outline, new Point(80, 143), 32, 26);
        d.DrawEllipse(Cream, null, new Point(80, 149), 18, 16);
        if (Species == "rabbit")
        {
            d.DrawEllipse(fur, Outline, new Point(61, 83), 11, 32);
            d.DrawEllipse(fur, Outline, new Point(96, 81), 11, 33);
            d.DrawEllipse(Pink, null, new Point(61, 80), 5, 22);
            d.DrawEllipse(Pink, null, new Point(96, 78), 5, 22);
        }
        else
        {
            Shape(d, "M 43,105 L 43,66 Q 45,61 51,66 L 73,85 Z", fur, Outline);
            Shape(d, "M 88,85 L 109,64 Q 115,62 116,69 L 116,105 Z", fur, Outline);
            Shape(d, "M 49,85 L 49,73 L 62,84 Z", Pink);
            Shape(d, "M 99,84 L 110,72 L 110,91 Z", Pink);
        }
        d.DrawEllipse(fur, Outline, new Point(80, 110), 43, 33);
        if (Species == "cat")
        {
            var stripe = new Pen(B("#DDB58C"), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            d.DrawLine(stripe, new Point(73, 81), new Point(74, 89));
            d.DrawLine(stripe, new Point(84, 80), new Point(84, 90));
            d.DrawLine(stripe, new Point(95, 83), new Point(93, 90));
        }
        d.DrawEllipse(Pink, null, new Point(51, 118), 8, 4);
        d.DrawEllipse(Pink, null, new Point(109, 118), 8, 4);
        if (Sleeping || Math.Sin(Phase * 1.3) > .995)
        {
            d.DrawGeometry(null, Outline, Geometry.Parse("M 59,108 Q 64,113 69,108 M 91,108 Q 96,113 101,108"));
        }
        else
        {
            d.DrawEllipse(Ink, null, new Point(64, 108), 3.2, 4.4);
            d.DrawEllipse(Ink, null, new Point(96, 108), 3.2, 4.4);
            d.DrawEllipse(Cream, null, new Point(65, 107), 1, 1);
            d.DrawEllipse(Cream, null, new Point(97, 107), 1, 1);
        }
        Shape(d, "M 77,117 Q 80,114 83,117 L 80,120 Z", Pink);
        d.DrawGeometry(null, new Pen(Ink, 1.6), Geometry.Parse("M 80,120 Q 75,126 72,121 M 80,120 Q 84,126 88,121"));
        d.DrawRoundedRectangle(Teal, null, new Rect(60, 135, 40, 6), 3, 3);
        d.DrawEllipse(B("#F4CC72"), new Pen(Ink, 1), new Point(81, 141), 4, 5);
        if (FaceLeft) d.Pop();
        d.Pop(); d.Pop(); d.Pop();
    }
}

