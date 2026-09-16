using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace TaskbarTails;

public sealed class App : Application
{
    public PetState State = new();
    public bool Exiting, PetsVisible = true;
    public MainWindow Panel = null!;
    readonly List<PetWindow> pets = new();
    Forms.NotifyIcon? tray;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly Stopwatch clock = new();
    double last, saved, refreshed, fullscreenChecked;
    bool fullscreen;
    Mutex? mutex;
    static string? smokePath;
    static int smokeExitCode;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "--smoke-test") smokePath = Path.GetFullPath(args[1]);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception.ToString()); } catch { }
            if (smokePath == null) MessageBox.Show("앱 실행 중 오류가 발생했습니다. 프로그램 폴더의 crash.log를 확인해 주세요.", "Taskbar Tails");
            e.Handled = true; app.Shutdown(1);
        };
        int result = app.Run(); return smokeExitCode == 0 ? result : smokeExitCode;
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (smokePath == null)
        {
            mutex = new Mutex(true, "Local\\TaskbarTails.Prototype", out bool first);
            if (!first) { MessageBox.Show("이미 실행 중이에요. 트레이 아이콘 또는 캐릭터를 더블클릭해 주세요.", "Taskbar Tails"); Shutdown(); return; }
        }
        if (smokePath != null) { Directory.CreateDirectory(smokePath); StateStore.PathName = Path.Combine(smokePath, "test-state.json"); }
        State = smokePath == null ? StateStore.Load() : new PetState();
        Panel = new MainWindow(this); MainWindow = Panel;
        var work = Native.Primary().Work;
        pets.Add(new PetWindow(this, State.Name, State.Species, "#FFF2DE", work.Left + (work.Right - work.Left) * .55));
        pets[0].Show(); pets[0].Say("안녕! 만나서 반가워");
        if (State.DemoFriends) SetFriends(true);
        SetupTray(); Panel.Show();
        timer.Tick += (_, _) => Tick(); clock.Start(); timer.Start();
        if (smokePath != null)
        {
            var smokeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            smokeTimer.Tick += (_, _) => { smokeTimer.Stop(); RunSmoke(); }; smokeTimer.Start();
        }
    }
    void SetupTray()
    {
        // Small original tray glyph. Clone before releasing the native icon handle.
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var green = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(54, 118, 95));
            using var cream = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 242, 222));
            g.FillEllipse(green, 0, 0, 31, 31); g.FillEllipse(cream, 7, 10, 18, 16);
            g.FillPolygon(cream, new[] { new System.Drawing.Point(7, 16), new System.Drawing.Point(7, 5), new System.Drawing.Point(15, 12) });
            g.FillPolygon(cream, new[] { new System.Drawing.Point(18, 12), new System.Drawing.Point(25, 5), new System.Drawing.Point(25, 16) });
            g.FillEllipse(green, 11, 17, 3, 4); g.FillEllipse(green, 19, 17, 3, 4);
        }
        var iconHandle = bitmap.GetHicon();
        using var temporaryIcon = System.Drawing.Icon.FromHandle(iconHandle);
        var icon = (System.Drawing.Icon)temporaryIcon.Clone(); DestroyIcon(iconHandle);
        tray = new Forms.NotifyIcon { Icon = icon, Text = "Taskbar Tails · 작은 친구들", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("친구 관리 열기", null, (_, _) => Dispatcher.Invoke(ShowPanel));
        menu.Items.Add("먹이 주기", null, (_, _) => Dispatcher.Invoke(Feed));
        menu.Items.Add("캐릭터 숨기기 / 보이기", null, (_, _) => Dispatcher.Invoke(ToggleVisible));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowPanel);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Min(.1, now - last); last = now;
        State.Tick(dt);
        if (now - fullscreenChecked > 1) { fullscreen = Native.FullscreenApp(); fullscreenChecked = now; SyncVisibility(); }
        foreach (var pet in pets) if (pet.IsVisible) pet.Step(dt);
        Panel.Animate(now);
        if (now - refreshed > .5) { Panel.Refresh(); refreshed = now; }
        if (now - saved > 15) { StateStore.Save(State); saved = now; }
    }
    public void ShowPanel() { Panel.Show(); Panel.WindowState = WindowState.Normal; Panel.Activate(); }
    public void Changed(string message) { StateStore.Save(State); pets[0].Say(message); Panel.Refresh(); }
    public void Feed() { State.Feed(); Changed("냠냠, 맛있다!"); }
    public void Pet() { State.Pet(); Changed("쓰담쓰담, 좋아요 ♥"); }
    public void Play() { State.Play(); pets[0].Bounce(); Changed("폴짝! 같이 놀자!"); }
    public void ToggleSleep() { State.Sleeping = !State.Sleeping; Changed(State.Sleeping ? "잘 자요… z Z" : "잘 잤다! 좋은 아침!"); }
    public void ToggleVisible() { PetsVisible = !PetsVisible; SyncVisibility(); Panel.Refresh(); }
    void SyncVisibility() { foreach (var p in pets) { bool show = PetsVisible && !fullscreen; if (show && !p.IsVisible) p.Show(); else if (!show && p.IsVisible) p.Hide(); } }
    public void SetFriends(bool enabled)
    {
        State.DemoFriends = enabled;
        if (enabled && pets.Count == 1)
        {
            var w = Native.Primary().Work; var span = w.Right - w.Left;
            pets.Add(new PetWindow(this, "복숭아 · 데모", "cat", "#F3C7AF", w.Left + span * .3, true));
            pets.Add(new PetWindow(this, "구름 · 데모", "rabbit", "#E7E4F5", w.Left + span * .75, true));
        }
        else if (!enabled) { foreach (var p in pets.Where(p => p.IsDemo).ToArray()) { p.Close(); pets.Remove(p); } }
        SyncVisibility(); StateStore.Save(State);
    }
    public void Quit()
    {
        if (Exiting) return;
        Exiting = true; timer.Stop(); StateStore.Save(State);
        if (tray != null) { tray.Visible = false; tray.Icon?.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        foreach (var p in pets) p.Close(); Panel.Close(); Shutdown();
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { StateStore.Save(State); base.OnSessionEnding(e); }
    protected override void OnExit(ExitEventArgs e) { mutex?.Dispose(); base.OnExit(e); }
    void RunSmoke()
    {
        var checks = new List<string>();
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("FAIL: " + label); checks.Add("PASS: " + label); }
        try
        {
            timer.Stop();
            Check(pets.Count == 1 && pets[0].IsVisible, "pet overlay created");
            Check(pets[0].InsideWorkArea(), "overlay aligned with primary work-area bottom");
            var exp = State.Experience; Feed(); Check(State.Experience == exp + 5, "feed increases XP");
            State.Fullness = 99; Feed(); Check(State.Fullness == 100, "fullness capped at 100");
            Play(); Check(!State.Sleeping, "play wakes pet");
            ToggleSleep(); Check(State.Sleeping, "sleep toggles on"); ToggleSleep();
            SetFriends(true); Check(pets.Count == 3, "two local demo friends added");
            SetFriends(true); Check(pets.Count == 3, "demo toggle is idempotent");
            ToggleVisible(); Check(pets.All(p => !p.IsVisible), "hide all overlays");
            ToggleVisible(); Check(pets.All(p => p.IsVisible), "show all overlays");
            State.Species = "rabbit"; pets[0].Step(.033); Check(pets[0].Visual.Species == "rabbit", "species applied to overlay");
            State.Species = "cat"; State.Name = "모찌"; State.Fullness = 86; State.Happiness = 94;
            StateStore.Save(State); var loaded = StateStore.Load(); Check(loaded.Name == State.Name && loaded.Experience == State.Experience, "state persistence round-trip");
            Panel.FriendsCheck.IsChecked = true; Panel.Refresh(); Panel.UpdateLayout();
            Export(Panel, Path.Combine(smokePath!, "preview.png"));
            foreach (var p in pets) { p.Step(.033); p.Visual.Bubble = ""; p.Visual.InvalidateVisual(); p.UpdateLayout(); }
            Export(pets[0], Path.Combine(smokePath!, "pet.png"));
            SetFriends(false); Check(pets.Count == 1, "demo friends removed");
            Panel.Close(); Check(!Panel.IsVisible && pets[0].IsVisible, "closing panel preserves companion");
            ShowPanel(); Check(Panel.IsVisible, "panel reopens");
            File.WriteAllLines(Path.Combine(smokePath!, "smoke-results.txt"), checks);
            Quit();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(smokePath!, "smoke-results.txt"), string.Join(Environment.NewLine, checks) + Environment.NewLine + ex);
            smokeExitCode = 1; Quit();
        }
    }
    static void Export(Window window, string file)
    {
        var content = (FrameworkElement)window.Content; content.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(file); encoder.Save(stream);
    }
}

