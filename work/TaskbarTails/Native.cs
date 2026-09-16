using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TaskbarTails;

public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; public bool IsPrimary => (Flags & 1) != 0; }
    [StructLayout(LayoutKind.Sequential)] public struct LastInputInfo { public uint Size, Time; }
    [StructLayout(LayoutKind.Sequential)] public struct AppBarData { public uint Size; public IntPtr Hwnd; public uint Callback, Edge; public Rect Rect; public int LParam; }
    public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] public static extern int SetWindowLong(IntPtr handle, int index, int value);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    public const uint SwpNoActivate = 0x0010, SwpNoZOrder = 0x0004;
    public static readonly IntPtr TopMost = new(-1);

    public static MonitorInfo Primary()
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromPoint(new Point { X = 0, Y = 0 }, 1), ref info);
        return info;
    }
    // Primary monitor first, then the others in enumeration order. Never empty.
    public static List<MonitorInfo> Monitors()
    {
        var list = new List<MonitorInfo>();
        MonitorEnumProc collect = (IntPtr m, IntPtr _, ref Rect _, IntPtr _) => { var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() }; if (GetMonitorInfo(m, ref info)) list.Add(info); return true; };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, collect, IntPtr.Zero);
        GC.KeepAlive(collect);
        list.Sort((a, b) => b.IsPrimary.CompareTo(a.IsPrimary));
        if (list.Count == 0) list.Add(Primary());
        return list;
    }
    public static MonitorInfo Selected(int index) { var all = Monitors(); return index >= 0 && index < all.Count ? all[index] : all[0]; }
    // Bottom edge the characters stand on. With an auto-hide taskbar the work area reaches the screen bottom,
    // so the (hidden) taskbar height is subtracted for the monitor that hosts it.
    public static int GroundBottom(MonitorInfo m)
    {
        int bottom = m.Work.Bottom;
        try
        {
            var state = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
            if (((uint)SHAppBarMessage(4, ref state) & 1) == 0) return bottom;
            var pos = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
            if (SHAppBarMessage(5, ref pos) == UIntPtr.Zero || pos.Edge != 3) return bottom;
            if (pos.Rect.Left < m.Monitor.Right && pos.Rect.Right > m.Monitor.Left) bottom = m.Monitor.Bottom - Math.Max(0, pos.Rect.Bottom - pos.Rect.Top);
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException) { }
        return bottom;
    }
    public static double IdleSeconds()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.Time) / 1000.0;
    }
    public static bool FullscreenApp(MonitorInfo screen)
    {
        var handle = GetForegroundWindow();
        var name = new System.Text.StringBuilder(256); GetClassName(handle, name, 256);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(handle, out var r)) return false;
        var m = screen.Monitor;
        return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }
}
