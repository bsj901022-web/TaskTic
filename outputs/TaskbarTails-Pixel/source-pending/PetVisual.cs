using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TaskbarTails;

// PixelLab sprites only. The preview is large; desktop pets are drawn at whole (or half) device pixels per sprite pixel
// so the pixel art stays crisp: 100% = 1 device pixel per sprite pixel, 150% = 1.5, 200% = 2.
public sealed class PetVisual : FrameworkElement
{
    public string Species = "cat";
    public string PetName = "모찌";
    public bool Sleeping, FrontView, Walking, FaceLeft;
    public double Phase, Jump, ActionTime, Sway, BallX, BallY;
    public double Shake;          // horizontal head-shake offset (too full to eat)
    public bool Balloon;          // floating up to a window top edge under a small balloon (code-native prop, like the parachute)
    public bool BubbleTyped;      // true: a typed message (speech bubble); false: something the character says by itself (thought cloud)
    public int Surface;           // 0 ground, 1 left screen edge (turned 90° clockwise), 2 right edge (90° anticlockwise), 3 ceiling (mirrored, feet up)
    public bool Parachute, BallVisible, GoldName, FaceFront;
    public int Level = 1;         // drives name colour tier, badge, sparkles (Lv.15) and crown (Lv.20); friends send theirs in events
    public double Celebrate;      // seconds of level-up sparkle burst left
    public string ActionKey = "";
    public string Bubble = "";
    public double SizeFactor = 1; // 1 = 100%, 1.5 = 150%, 2 = 200% (desktop overlay only)
    public int BubbleStyle;       // 0 cream, 1 mint, 2 lavender, 3 peach (unlocked by level)
    public int NameStyle = 1;     // 1 small text, 2 bold text on a rounded label (0 = hidden via ShowName)
    public bool ShowName { get; set; } = true;
    static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    static readonly Brush Ink = B("#49403C"), Gold = B("#B8860B"), Teal = B("#367969"), Shadow = B("#19000000"), Rope = B("#766D61"), Ruby = B("#D9484E");
    static readonly Brush Royal = MakeRoyal();
    static Brush MakeRoyal() { var g = new LinearGradientBrush(Color.FromRgb(0xB8, 0x86, 0x0B), Color.FromRgb(0xC8, 0x4A, 0x8C), 0); g.Freeze(); return g; }
    static readonly Brush[] BubbleFill = { B("#FFFDF8"), B("#E3F4EC"), B("#ECE8F7"), B("#FDEBDD") };
    static readonly Pen[] BubbleEdge = { new(B("#DDD9CD"), 1), new(B("#A9D6C1"), 1), new(B("#C5BBE6"), 1), new(B("#EFC2A3"), 1) };
    static readonly Brush[] Canopy = { B("#8FC7AD"), B("#FFF1CB"), B("#EAA69E") };
    static readonly Pen RopePen = new(Rope, 1);
    static readonly Brush ThoughtInk = B("#6E655F");
    static readonly Pen ThoughtEdge = new(B("#B9B3A8"), 1) { DashStyle = DashStyles.Dash };
    static readonly Typeface Regular = new(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Typeface Bold = new(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    static readonly string[] CanopyRows = { "000001111100000", "000111111111000", "001111111111100", "011111111111110", "111111111111111", "111111111111111" };
    FormattedText? nameText; string nameKey = "";
    static PetVisual() { foreach (var p in BubbleEdge) p.Freeze(); RopePen.Freeze(); ThoughtEdge.Freeze(); }
    public PetVisual() { RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); RenderOptions.SetEdgeMode(this, EdgeMode.Aliased); }
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
    // Level rewards visible to everyone in the room: 0 plain, 1 teal (Lv.5), 2 gold (Lv.10), 3 gradient (Lv.20).
    int Tier => Level >= 20 ? 3 : Level >= 10 ? 2 : Level >= 5 ? 1 : 0;
    Brush TierBrush => Tier switch { 3 => Royal, 2 => Gold, 1 => Teal, _ => Ink };
    FormattedText? badgeText;
    // The name is drawn every frame, so its layout is cached until the text, level or DPI changes. From Lv.5 a small level badge follows the name.
    void DrawName(DrawingContext d, double y)
    {
        bool large = NameStyle >= 2 && !FrontView, bold = large || Tier >= 2;
        string key = PetName + "|" + Level + "|" + large + "|" + VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (nameText == null || key != nameKey) { nameText = Make(PetName, large ? 12 : 10, TierBrush, bold); badgeText = Level >= 5 ? Make(Level.ToString(), 7.5, Brushes.White, true) : null; nameKey = key; }
        double badgeW = badgeText == null ? 0 : Math.Ceiling(badgeText.Width) + 7, gap = badgeText == null ? 0 : 4;
        double total = nameText.Width + gap + badgeW, left = 80 - total / 2;
        if (large)
        {
            // Readable on any wallpaper: bold text on an opaque rounded label, nudged up so it clears the sprite.
            double w = Math.Ceiling(total) + 14, h = Math.Ceiling(nameText.Height) + 4;
            var box = new Rect(80 - w / 2, y - 3, w, h);
            d.DrawRoundedRectangle(BubbleFill[0], BubbleEdge[0], box, h / 2, h / 2);
            d.DrawText(nameText, new Point(left, y - 1));
            if (badgeText != null) DrawBadge(d, left + nameText.Width + gap, y - 1 + (nameText.Height - 11) / 2, badgeW);
            return;
        }
        d.DrawText(nameText, new Point(left, y));
        if (badgeText != null) DrawBadge(d, left + nameText.Width + gap, y + (nameText.Height - 11) / 2, badgeW);
    }
    void DrawBadge(DrawingContext d, double x, double y, double w)
    {
        d.DrawRoundedRectangle(TierBrush, null, new Rect(x, y, w, 11), 5.5, 5.5);
        d.DrawText(badgeText, new Point(x + (w - badgeText!.Width) / 2, y + (11 - badgeText.Height) / 2));
    }
    // Small twinkling pixel stars around the character: two of them from Lv.15, a burst of six for three seconds after a level-up.
    void DrawSparkles(DrawingContext d, double cx, double cy, double rx, double ry, int count, double size, double speed)
    {
        for (int i = 0; i < count; i++)
        {
            double a = Phase * speed + i * (Math.PI * 2 / count), tw = (Math.Sin(Phase * 5 + i * 1.7) + 1) / 2, len = size * (.45 + .55 * tw);
            double x = Math.Round(cx + Math.Cos(a) * rx), y = Math.Round(cy + Math.Sin(a) * ry);
            var brush = tw > .75 ? Brushes.White : Gold;
            d.DrawRectangle(brush, null, new Rect(x - len, y - .5, len * 2, 1));
            d.DrawRectangle(brush, null, new Rect(x - .5, y - len, 1, len * 2));
        }
    }
    // Original pixel crown for Lv.20 and up, drawn above the head (7x4 cells).
    static readonly string[] CrownRows = { "1010101", "1111111", "1111111", "0111110" };
    void DrawCrown(DrawingContext d, double top, double cell)
    {
        double left = 80 - 3.5 * cell;
        for (int r = 0; r < CrownRows.Length; r++) for (int c = 0; c < 7; c++) if (CrownRows[r][c] == '1')
            d.DrawRectangle(r == 1 && c == 3 ? Ruby : Gold, null, new Rect(left + c * cell, top + r * cell, cell, cell));
    }
    // Grows upward from the pet and wraps long text (up to 80 characters) inside the 160px overlay.
    // Typed messages are speech bubbles (bold text, pointed tail); the character's own lines are thought clouds (lighter text, dashed edge, two little circles).
    // cx: horizontal centre of the tail; below: the bubble hangs under the anchor (ceiling) instead of rising above it.
    void DrawBubble(DrawingContext d, double anchor, double minTop, double cx = 80, bool below = false)
    {
        bool typed = BubbleTyped;
        var f = Make(Bubble, typed ? 10 : 9.5, typed ? Ink : ThoughtInk, typed, 128);
        double width = Math.Min(150, Math.Ceiling(f.Width) + 16), height = Math.Ceiling(f.Height) + 10;
        double left = Math.Clamp(cx - width / 2, -26, 186 - width);
        var box = below ? new Rect(left, anchor + (typed ? 6 : 10), width, height) : new Rect(left, Math.Max(minTop, anchor - height - (typed ? 0 : 7)), width, height);
        int style = Math.Clamp(BubbleStyle, 0, BubbleFill.Length - 1);
        double tailY = below ? box.Top : box.Bottom, dir = below ? -1 : 1;
        if (typed)
        {
            d.DrawRoundedRectangle(BubbleFill[style], BubbleEdge[style], box, 10, 10);
            var tail = new StreamGeometry();
            using (var g = tail.Open()) { g.BeginFigure(new Point(cx - 4, tailY - dir), true, true); g.LineTo(new Point(cx, tailY + 5 * dir), true, false); g.LineTo(new Point(cx + 4, tailY - dir), true, false); }
            tail.Freeze(); d.DrawGeometry(BubbleFill[style], null, tail);
        }
        else
        {
            d.PushOpacity(.9);
            d.DrawRoundedRectangle(BubbleFill[style], ThoughtEdge, box, 14, 14);
            d.DrawEllipse(BubbleFill[style], ThoughtEdge, new Point(cx - 2, tailY + 4 * dir), 2.8, 2.8);
            d.DrawEllipse(BubbleFill[style], ThoughtEdge, new Point(cx - 4.5, tailY + 9.5 * dir), 1.7, 1.7);
            d.Pop();
        }
        d.DrawText(f, new Point(box.Left + 8, box.Top + 5));
    }
    // Device pixels per sprite pixel for the desktop overlay: whole or half steps, never below 1 (downscaling breaks pixel art).
    public static double DevicePixelsPerSprite(double sizeFactor) => Math.Max(1, Math.Round(sizeFactor * 2) / 2);
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        var sprites = SpriteLibrary.Get(Species);
        if (sprites == null) return;
        // Desktop pets draw on a 160x220 canvas whose ground line stays at y=174; the extra 40px on top gives
        // 150%/200% characters and tall speech bubbles room without moving the feet off the taskbar.
        double canvasHeight = FrontView ? 180 : 220, headroom = FrontView ? 0 : 40;
        double scale = Math.Min(ActualWidth / 160, ActualHeight / canvasHeight);
        double dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        d.PushTransform(new TranslateTransform((ActualWidth - 160 * scale) / 2, (ActualHeight - canvasHeight * scale) / 2 + headroom * scale));
        d.PushTransform(new ScaleTransform(scale, scale));
        // Preview: fit the card. Desktop: exact device-pixel multiples so every sprite pixel is a crisp square.
        double zoom = FrontView ? Math.Min(3, Math.Min(126 / sprites.Bounds.Width, 113 / sprites.Bounds.Height)) : DevicePixelsPerSprite(SizeFactor) / (dpiScale * scale);
        double snap = dpiScale * scale; // canvas units -> device pixels
        double petTop = 174 - sprites.Bounds.Height * zoom;
        double bodyLength = sprites.Bounds.Height * zoom, bodyWidth = sprites.Bounds.Width * zoom;
        if (Surface == 0) d.DrawEllipse(Shadow, null, new Point(80, 174), Math.Max(5, bodyWidth * .42 - Jump / 12), FrontView ? 4 : Math.Max(2, zoom * 2.2));
        if (Surface == 0)
        {
            // Lv.20+: crown above the head; the name and bubble move up to make room.
            double crownCell = Math.Max(1.5, zoom * 1.6), crownLift = Level >= 20 && !Parachute && !Balloon ? crownCell * 4 + 3 : 0;
            if (crownLift > 0) DrawCrown(d, petTop - crownLift + 1, crownCell);
            if (ShowName && !Parachute && !Balloon) DrawName(d, FrontView ? 119 : petTop - (NameStyle >= 2 ? 21 : 15) - crownLift);
            if (Bubble.Length > 0) DrawBubble(d, FrontView ? 40 : petTop - (ShowName && NameStyle >= 2 ? 26 : 18) - crownLift, FrontView ? 2 : 2 - headroom);
            else if (Sleeping) Text(d, "z z Z", 12, Teal, FrontView ? 108 : 80 + bodyWidth * .45, FrontView ? 38 : petTop - 30 - crownLift);
            if (Celebrate > 0) DrawSparkles(d, 80, petTop + bodyLength / 2, bodyWidth / 2 + 14, bodyLength / 2 + 8, 6, 5, 1.6);
            else if (Level >= 15) DrawSparkles(d, 80, petTop + bodyLength / 2, bodyWidth / 2 + 9, bodyLength / 2 + 4, 2, 3, .8);
        }
        else if (Surface == 3)
        {
            // Upside down under the top edge: name and bubble hang below the body.
            double bodyBottom = 6 + bodyLength;
            if (ShowName) DrawName(d, bodyBottom + 6);
            if (Bubble.Length > 0) DrawBubble(d, bodyBottom + (ShowName ? (NameStyle >= 2 ? 26 : 20) : 4), 0, 80, true);
        }
        else if (Bubble.Length > 0)
        {
            // Sideways on a wall: the body lies horizontally from the feet; the bubble sits above its middle.
            double cx = 80 + (Surface == 1 ? 1 : -1) * bodyLength / 2;
            DrawBubble(d, 74 - bodyWidth / 2 - 12, 2 - headroom, cx);
        }
        var (frame, flip) = sprites.Frame(FaceLeft, Walking && !Sleeping && !FaceFront, Phase, FrontView || (FaceFront && ActionKey.Length == 0), ActionKey, ActionTime);
        double jumpOffset = FrontView ? Jump : Jump * zoom / 2.2;
        double px = 80 - (sprites.Bounds.Left + sprites.Bounds.Width / 2) * zoom;
        double py = 174 - sprites.Bounds.Bottom * zoom - jumpOffset;
        if (!FrontView) { px = Math.Round(px * snap) / snap; py = Math.Round(py * snap) / snap; }
        if (Shake != 0) d.PushTransform(new TranslateTransform(Shake, 0));
        // Walls turn the sprite about the feet (feet stay on the edge); the ceiling mirrors it vertically and moves it up so the feet sit at canvas y=6.
        // On walls the body lies horizontally at foot height, so it is also moved up 100 (feet at canvas y=74) to stay inside the window.
        if (Surface == 1) { d.PushTransform(new TranslateTransform(0, -100)); d.PushTransform(new RotateTransform(90, 80, 174)); }
        else if (Surface == 2) { d.PushTransform(new TranslateTransform(0, -100)); d.PushTransform(new RotateTransform(-90, 80, 174)); }
        else if (Surface == 3) { d.PushTransform(new TranslateTransform(0, -168)); d.PushTransform(new ScaleTransform(1, -1, 0, 174)); }
        if (Parachute || Balloon) d.PushTransform(new RotateTransform(Sway, 80, petTop + 6));
        if (flip) d.PushTransform(new ScaleTransform(-1, 1, 80, 0));
        d.DrawImage(frame, new Rect(px, py, frame.PixelWidth * zoom, frame.PixelHeight * zoom));
        if (flip) d.Pop();
        if (Parachute)
        {
            // Small code-native pixel prop; all characters themselves come from PixelLab.
            // Drawn relative to the head (designed for a pet whose top is at y=132) and scaled with the character.
            double propScale = Math.Max(1, zoom / 0.75);
            d.PushTransform(new TranslateTransform(0, petTop - 132));
            d.PushTransform(new ScaleTransform(propScale, propScale, 80, 142));
            d.DrawLine(RopePen, new Point(55, 117), new Point(76, 142));
            d.DrawLine(RopePen, new Point(105, 117), new Point(84, 142));
            d.DrawLine(RopePen, new Point(80, 114), new Point(80, 142));
            for (int row = 0; row < CanopyRows.Length; row++) for (int col = 0; col < CanopyRows[row].Length; col++) if (CanopyRows[row][col] == '1')
                d.DrawRectangle(Canopy[col < 5 ? 0 : col < 10 ? 1 : 2], null, new Rect(54 + col * 3.5, 96 + row * 3.5, 3.5, 3.5));
            d.Pop(); d.Pop();
        }
        if (Balloon)
        {
            // The parachute's counterpart for going up: a string from the hand and a small peach balloon, scaled like the parachute.
            double propScale = Math.Max(1, zoom / 0.75);
            d.PushTransform(new TranslateTransform(0, petTop - 132));
            d.PushTransform(new ScaleTransform(propScale, propScale, 80, 142));
            d.DrawLine(RopePen, new Point(80, 142), new Point(80, 106));
            d.DrawRectangle(Canopy[2], null, new Rect(78.5, 103, 3, 3));
            d.DrawEllipse(Canopy[2], null, new Point(80, 92), 8.5, 10.5);
            d.DrawEllipse(Canopy[1], null, new Point(76.5, 87), 2, 2.6);
            d.Pop(); d.Pop();
        }
        if (Parachute || Balloon) d.Pop();
        if (Surface != 0) { d.Pop(); d.Pop(); }
        if (Shake != 0) d.Pop();
        d.Pop(); d.Pop();
    }
}
