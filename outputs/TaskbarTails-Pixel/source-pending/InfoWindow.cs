using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TaskbarTails;

// About · Contact side panel. Docks to the right edge of the main panel (left if there is no room),
// follows it when it moves or resizes, and hides when the panel hides. Closing just hides it.
public sealed class InfoWindow : Window
{
    const string ContactEmail = "bsj_2200@naver.com", Instagram = "Rinsomnia__", Repo = "https://github.com/bsj901022-web/TaskTic";
    readonly App app;
    readonly MainWindow panel;
    readonly StackPanel body = new() { Margin = new Thickness(20, 14, 20, 20) };
    readonly TextBlock notice = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(126, 138, 128)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    public int LinkCount { get; private set; }
    public bool AllowClose;
    public InfoWindow(App owner, MainWindow host)
    {
        app = owner; panel = host;
        Title = L.Get("about_title"); Width = 320; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false;
        Background = new SolidColorBrush(Color.FromRgb(247, 247, 242)); FontFamily = new FontFamily("Malgun Gothic"); Foreground = new SolidColorBrush(Color.FromRgb(39, 62, 55));
        Owner = host;
        var header = new Grid { Background = new SolidColorBrush(Color.FromRgb(232, 238, 220)), Height = 46 };
        header.Children.Add(new TextBlock { Text = L.Get("about_title"), FontWeight = FontWeights.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) });
        var close = new Button { Content = "✕", Width = 34, Height = 30, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 13, Cursor = System.Windows.Input.Cursors.Hand };
        close.Click += (_, _) => Hide(); header.Children.Add(close);
        var root = new DockPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(200, 210, 198)), BorderThickness = new Thickness(1), Child = root };
        Build();
        Closing += (_, e) => { if (!app.Exiting && !AllowClose) { e.Cancel = true; Hide(); } };
        panel.LocationChanged += (_, _) => Reposition();
        panel.SizeChanged += (_, _) => Reposition();
        panel.StateChanged += (_, _) => Reposition();
        panel.IsVisibleChanged += (_, _) => { if (!panel.IsVisible) Hide(); };
    }
    void Build()
    {
        body.Children.Clear(); LinkCount = 0;
        body.Children.Add(new TextBlock { Text = "Taskbar Tails " + app.VersionLabel + (StateStore.IsInstalled ? "" : L.Get("badge_portable")), FontWeight = FontWeights.Bold, FontSize = 13 });
        body.Children.Add(new TextBlock { Text = L.F("about_made", app.VersionLabel), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(126, 138, 128)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        Section(L.Get("info_section_contact"), L.Get("about_desc"));
        Link(L.F("contact_email", ContactEmail), () => OpenExternal("mailto:" + ContactEmail + "?subject=" + Uri.EscapeDataString("Taskbar Tails " + app.VersionLabel)), true);
        Link(L.Get("contact_copy_email"), () => { Clipboard.SetText(ContactEmail); notice.Text = L.F("copied", ContactEmail); });
        Link(L.F("contact_instagram", Instagram), () => OpenExternal("https://www.instagram.com/" + Instagram + "/"));
        Section(L.Get("info_section_project"), null);
        Link(L.Get("contact_github"), () => OpenExternal(Repo));
        Link(L.Get("contact_issues"), () => OpenExternal(Repo + "/issues"));
        Link(L.Get("contact_releases"), () => OpenExternal(Repo + "/releases"));
        Section(L.Get("info_section_folders"), null);
        Link(L.Get("open_data"), () => OpenFolder(Path.GetDirectoryName(StateStore.PathName)!));
        Link(L.Get("open_logs"), () => OpenFolder(AppContext.BaseDirectory));
        Section(L.Get("info_section_help"), L.F("info_help_text", Hotkeys.Label(app.State.ChatHotkey), Hotkeys.Label(app.State.HideHotkey), Hotkeys.Label(app.State.BubbleHotkey)));
        body.Children.Add(notice);
        body.Children.Add(new TextBlock { Text = L.Get("info_dock_hint"), FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(150, 157, 148)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) });
    }
    void Section(string title, string? text)
    {
        body.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 16, 0, 6), Foreground = new SolidColorBrush(Color.FromRgb(78, 130, 105)) });
        if (text != null) body.Children.Add(new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(113, 129, 107)), Margin = new Thickness(0, 0, 0, 6) });
    }
    void Link(string label, Action action, bool primary = false)
    {
        var b = new Button { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 6), FontSize = 12, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(primary ? Color.FromRgb(54, 118, 95) : Color.FromRgb(234, 240, 233)), Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(40, 89, 72)) };
        b.Click += (_, _) => { try { action(); } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { notice.Text = L.F("open_failed", e.Message); } };
        body.Children.Add(b); LinkCount++;
    }
    static void OpenExternal(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    static void OpenFolder(string path) { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); }
    public void ShowDocked() { Build(); Show(); Reposition(); }
    // Right edge of the panel by default; left edge when the right side would leave the work area.
    public void Reposition()
    {
        if (!IsVisible) return;
        if (!panel.IsVisible || panel.WindowState == WindowState.Minimized) { Hide(); return; }
        double dpi = VisualTreeHelper.GetDpi(panel).DpiScaleX;
        double workRight = app.Screen.Work.Right / dpi, workLeft = app.Screen.Work.Left / dpi;
        double left = panel.Left + panel.ActualWidth;
        if (left + Width > workRight && panel.Left - Width >= workLeft) left = panel.Left - Width;
        Left = left; Top = panel.Top; Height = Math.Max(420, panel.ActualHeight);
    }
    public bool IsDockedBeside => IsVisible && (Math.Abs(Left - (panel.Left + panel.ActualWidth)) < 1 || Math.Abs(Left + Width - panel.Left) < 1) && Math.Abs(Top - panel.Top) < 1;
}
