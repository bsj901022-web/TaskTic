using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TaskbarTails;

public sealed class PetWindow : Window
{
    public readonly PetVisual Visual = new();
    readonly App app;
    readonly bool demo;
    readonly Random random = new();
    IntPtr handle;
    double x, phase, decision, leap, bubbleUntil;
    int direction = 1;
    bool walking = true, dragging, moved;
    int dragStart, windowStart;
    double dpi = 1;
    public bool IsDemo => demo;
    public PetWindow(App owner, string name, string species, string coat, double initialX, bool isDemo = false)
    {
        app = owner; demo = isDemo; x = initialX;
        Width = 160; Height = 180;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        ResizeMode = ResizeMode.NoResize; Title = "Taskbar Tails · " + name;
        Visual.PetName = name; Visual.Species = species; Visual.Coat = coat;
        Content = Visual; Cursor = Cursors.Hand;
        SourceInitialized += (_, _) => {
            handle = new WindowInteropHelper(this).Handle;
            Native.SetWindowLong(handle, -20, Native.GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            HwndSource.FromHwnd(handle)?.AddHook(Hook);
            Place();
        };
        MouseLeftButtonDown += (_, e) => {
            if (e.ClickCount == 2) { app.ShowPanel(); return; }
            Native.GetCursorPos(out var p); dragStart = p.X; windowStart = (int)x;
            dragging = true; moved = false; CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, _) => {
            if (!dragging) return;
            Native.GetCursorPos(out var p);
            if (Math.Abs(p.X - dragStart) > 5) moved = true;
            if (moved) { x = windowStart + p.X - dragStart; Place(); }
        };
        MouseLeftButtonUp += (_, _) => {
            if (!dragging) return;
            dragging = false; ReleaseMouseCapture();
            if (!moved) { if (!demo) app.Pet(); else Say("안녕! 나는 데모 친구야"); Bounce(); }
        };
        LostMouseCapture += (_, _) => dragging = false;
        var menu = new ContextMenu();
        void Item(string text, Action action) { var i = new MenuItem { Header = text }; i.Click += (_, _) => action(); menu.Items.Add(i); }
        Item("친구 관리 열기", app.ShowPanel);
        if (!demo) { Item("먹이 주기", app.Feed); Item("함께 놀기", app.Play); Item("잠자기 / 깨우기", app.ToggleSleep); }
        menu.Items.Add(new Separator()); Item("프로그램 종료", app.Quit);
        ContextMenu = menu;
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x21) { handled = true; return new IntPtr(3); }
        return IntPtr.Zero;
    }
    public void Say(string message) { Visual.Bubble = message; bubbleUntil = phase + 3; }
    public void Bounce() { leap = .01; }
    public void Step(double dt)
    {
        phase += dt;
        if (!demo) { Visual.PetName = app.State.Name; Visual.Species = app.State.Species; Visual.Sleeping = app.State.Sleeping; }
        if (phase >= bubbleUntil) Visual.Bubble = "";
        if (phase > decision)
        {
            walking = random.NextDouble() > .3;
            direction = random.NextDouble() > .5 ? 1 : -1;
            decision = phase + random.Next(3, 8);
        }
        if (walking && !Visual.Sleeping && !dragging && !IsMouseOver) x += direction * dt * 36 * dpi;
        if (leap > 0) { leap += dt; if (leap > .65) leap = 0; }
        Visual.Jump = leap == 0 ? 0 : Math.Sin(leap / .65 * Math.PI) * 39;
        Visual.Phase = phase; Visual.Walking = walking && !dragging && !IsMouseOver; Visual.FaceLeft = direction < 0;
        Place(); Visual.InvalidateVisual();
    }
    void Place()
    {
        if (handle == IntPtr.Zero) return;
        var work = Native.Primary().Work;
        dpi = Math.Max(1, Native.GetDpiForWindow(handle) / 96.0);
        var width = (int)Math.Round(Width * dpi); var height = (int)Math.Round(Height * dpi);
        var max = Math.Max(work.Left, work.Right - width);
        if (x < work.Left) { x = work.Left; direction = 1; }
        if (x > max) { x = max; direction = -1; }
        Native.SetWindowPos(handle, new IntPtr(-1), (int)x, work.Bottom - height, width, height, 0x0010);
    }
    public bool InsideWorkArea()
    {
        if (!Native.GetWindowRect(handle, out var r)) return false;
        var w = Native.Primary().Work;
        return r.Left >= w.Left && r.Right <= w.Right && Math.Abs(r.Bottom - w.Bottom) <= 2;
    }
}
