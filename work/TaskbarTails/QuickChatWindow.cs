using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TaskbarTails;

// A small input bubble that pops up right above the character (Ctrl+Alt+T, middle-click, tray menu).
// Enter sends, Esc or clicking elsewhere closes. No window to manage.
public sealed class QuickChatWindow : Window
{
    readonly App app;
    public bool AllowClose;
    readonly TextBox box = new() { Width = 250, MaxLength = 80, Padding = new Thickness(8, 6, 8, 6), FontSize = 13, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    readonly TextBlock hint = new() { Foreground = new SolidColorBrush(Color.FromRgb(160, 165, 158)), FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    public static readonly string[] Reactions = { "❤️", "👋", "😂", "👍", "😢", "🎉" };
    public QuickChatWindow(App owner)
    {
        app = owner;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight; ShowActivated = true; FontFamily = new FontFamily("Malgun Gothic");
        hint.Text = L.Get("qc_placeholder");
        var input = new Grid(); input.Children.Add(box); input.Children.Add(hint);
        var inputBorder = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), BorderBrush = new SolidColorBrush(Color.FromRgb(221, 227, 216)), BorderThickness = new Thickness(1), Child = input };
        var reactions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var r in Reactions)
        {
            var b = new Button { Content = r, FontSize = 15, Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(0, 0, 4, 0), Background = new SolidColorBrush(Color.FromRgb(234, 240, 233)), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            b.Click += (_, _) => { app.SendBubble(r); Hide(); };
            reactions.Children.Add(b);
        }
        var stack = new StackPanel(); stack.Children.Add(inputBorder); stack.Children.Add(reactions);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(247, 247, 242)), CornerRadius = new CornerRadius(14), Padding = new Thickness(10), BorderBrush = new SolidColorBrush(Color.FromRgb(200, 210, 198)), BorderThickness = new Thickness(1), Child = stack,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = .25 } };
        box.TextChanged += (_, _) => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { var text = box.Text.Trim(); if (text.Length > 0) app.SendBubble(text); box.Clear(); Hide(); e.Handled = true; }
            else if (e.Key == Key.Escape) { box.Clear(); Hide(); e.Handled = true; }
        };
        Deactivated += (_, _) => { if (!app.IsSmokeTest) Hide(); };
        Closing += (_, e) => { if (!app.Exiting && !AllowClose) { e.Cancel = true; Hide(); } };
    }
    // screenX/screenTop are physical pixels: the horizontal centre of the pet and the top of its overlay window.
    public void ShowAt(double screenX, double screenTop, Native.MonitorInfo screen)
    {
        hint.Text = L.Get("qc_placeholder");
        Show(); UpdateLayout();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double width = ActualWidth * scale, height = ActualHeight * scale;
        double left = Math.Clamp(screenX - width / 2, screen.Work.Left, Math.Max(screen.Work.Left, screen.Work.Right - width));
        double top = Math.Max(screen.Work.Top, screenTop - height + 34 * scale);
        Left = left / scale; Top = top / scale;
        Activate(); box.Focus(); Keyboard.Focus(box);
    }
}
