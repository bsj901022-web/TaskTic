using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarTails;

public sealed class DesktopWindow
{
    public IntPtr Handle; public Native.Rect Bounds; public string Title = ""; public string Class = ""; public bool Foreground;
    public bool IsTest => Handle == Desktop.TestHandle;
}

// Other programs' top-level windows on the character's monitor. Their top edges are platforms the character can land on and
// walk along; side edges that reach the ground can be climbed. Kept deliberately light: one EnumWindows pass (no accessibility
// API, nothing inside the windows) at most 4 times a second while a character is airborne or on a window, otherwise every 2 s,
// and a title-only poll of the foreground window. Positions are physical pixels (DWM extended frame bounds, so invisible resize
// borders do not count).
public static class Desktop
{
    public static readonly IntPtr TestHandle = new(-7);
    // Smoke test: never treat real windows as platforms, only the injected test window.
    public static bool TestOnly;
    static List<DesktopWindow> windows = new();
    static double scannedAt = double.NegativeInfinity;
    static Native.Rect? testPlatform;
    public static IReadOnlyList<DesktopWindow> Windows => windows;
    // Increments on every scan so callers can skip lookups while nothing changed.
    public static int Version { get; private set; }

    public static void Scan(Native.MonitorInfo screen, double now, double interval = .25, bool force = false)
    {
        if (!force && now - scannedAt < interval) return; scannedAt = now;
        var list = new List<DesktopWindow>();
        if (!TestOnly)
        {
            var fg = Native.GetForegroundWindow(); int me = Environment.ProcessId; var m = screen.Monitor;
            Native.EnumWindowsProc collect = (h, _) =>
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                Native.GetWindowThreadProcessId(h, out uint pid); if (pid == me) return true;          // our own overlays and panels
                if ((Native.GetWindowLong(h, -20) & 0x80) != 0) return true;                             // tool windows
                if (Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true; // hidden UWP frames, other virtual desktops
                var cls = new StringBuilder(64); Native.GetClassName(h, cls, 64); string className = cls.ToString();
                if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow") return true;
                int len = Native.GetWindowTextLength(h); if (len == 0) return true;
                var text = new StringBuilder(len + 1); Native.GetWindowText(h, text, len + 1);
                if (Native.DwmGetWindowAttribute(h, 9, out Native.Rect r, Marshal.SizeOf<Native.Rect>()) != 0) Native.GetWindowRect(h, out r);
                if (r.Right - r.Left < 120 || r.Bottom - r.Top < 60) return true;
                if (r.Right <= m.Left || r.Left >= m.Right || r.Bottom <= m.Top || r.Top >= m.Bottom) return true;
                list.Add(new DesktopWindow { Handle = h, Bounds = r, Title = text.ToString(), Class = className, Foreground = h == fg });
                return true;
            };
            try { Native.EnumWindows(collect, IntPtr.Zero); } catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException) { }
            GC.KeepAlive(collect);
        }
        if (testPlatform != null) list.Add(new DesktopWindow { Handle = TestHandle, Bounds = testPlatform.Value, Title = "Test window" });
        windows = list; Version++;
    }
    // Immediate rescan without touching the throttle clock (used right before a climb decision and by the smoke test).
    public static void Refresh(Native.MonitorInfo screen) => Scan(screen, scannedAt, 0, true);
    public static DesktopWindow? Find(IntPtr handle) => windows.FirstOrDefault(w => w.Handle == handle);
    // Cheap: no enumeration, just the active window's title (empty for our own windows and the desktop).
    public static string ForegroundTitle()
    {
        if (TestOnly) return "";
        var h = Native.GetForegroundWindow(); if (h == IntPtr.Zero) return "";
        Native.GetWindowThreadProcessId(h, out uint pid); if (pid == Environment.ProcessId) return "";
        int len = Native.GetWindowTextLength(h); if (len == 0) return "";
        var text = new StringBuilder(len + 1); Native.GetWindowText(h, text, len + 1); return text.ToString();
    }
    // True when that point of the window is really on top, not hidden behind another window. Our own overlays never count as cover.
    public static bool Exposed(DesktopWindow w, double x, double y)
    {
        if (w.IsTest) return true;
        var hit = Native.WindowFromPoint(new Native.Point { X = (int)Math.Round(x), Y = (int)Math.Round(y) });
        if (hit == IntPtr.Zero) return false;
        var root = Native.GetAncestor(hit, 2);
        if (root == w.Handle) return true;
        Native.GetWindowThreadProcessId(root, out uint pid); return pid == Environment.ProcessId;
    }
    // The highest window top edge the feet crossed while falling from fromY to toY at horizontal position x; null = keep falling.
    // Edges within 40 px of the taskbar are ignored so the character simply lands on the ground there.
    public static DesktopWindow? PlatformBetween(double x, double fromY, double toY, int ground, int minTop, double margin)
    {
        foreach (var w in windows.OrderBy(w => w.Bounds.Top))
        {
            int top = w.Bounds.Top;
            if (top < minTop || top > ground - 40) continue;
            if (top < fromY - 1 || top > toY) continue;
            if (x < w.Bounds.Left + margin || x > w.Bounds.Right - margin) continue;
            if (!Exposed(w, x, top + 6)) continue;
            return w;
        }
        return null;
    }
    // The nearest window that reaches down to the ground and is tall enough to be worth climbing: (window, use its left edge?).
    public static (DesktopWindow? Window, bool LeftEdge) ClimbTarget(double centerX, int ground, Native.Rect work, int minTop, double dpi)
    {
        DesktopWindow? best = null; bool left = true; double bestDistance = 600 * dpi;
        foreach (var w in windows)
        {
            var b = w.Bounds;
            if (b.Bottom < ground - 30 * dpi || b.Top > ground - 200 * dpi || b.Top < minTop || b.Right - b.Left < 200) continue;
            foreach (bool edgeLeft in new[] { true, false })
            {
                if (!EdgeUsable(w, edgeLeft, ground, work, dpi)) continue;
                double wall = edgeLeft ? b.Left : b.Right, distance = Math.Abs(wall - centerX);
                if (distance < bestDistance) { bestDistance = distance; best = w; left = edgeLeft; }
            }
        }
        return (best, left);
    }
    // The edge lies inside the work area with room for the character beside it, and the window is visible along it.
    public static bool EdgeUsable(DesktopWindow w, bool leftEdge, int ground, Native.Rect work, double dpi)
    {
        var b = w.Bounds; double wall = leftEdge ? b.Left : b.Right;
        if (leftEdge ? wall - 80 * dpi < work.Left : wall + 80 * dpi > work.Right) return false;
        double inside = leftEdge ? wall + 4 : wall - 4;
        return Exposed(w, inside, ground - 40 * dpi) && Exposed(w, inside, (b.Top + ground) / 2.0);
    }
    public static void TestPlatform(Native.Rect? rect) { testPlatform = rect; }

    // What the foreground window seems to be about, as a string key ("win_video" ...), or null. Specific apps first, generic browsers last.
    static readonly (string Key, string[] Words)[] Topics =
    {
        ("win_video", new[] { "youtube", "netflix", "tving", "티빙", "twitch", "치지직", "chzzk", "watcha", "왓챠", "disney+", "wavve", "웨이브", "laftel", "라프텔", "coupang play", "쿠팡플레이", "vimeo" }),
        ("win_music", new[] { "spotify", "melon", "멜론", "apple music", "youtube music", "bugs", "벅스", "genie", "지니", "soundcloud" }),
        ("win_game", new[] { "steam", "league of legends", "리그 오브 레전드", "battlegrounds", "배틀그라운드", "minecraft", "마인크래프트", "valorant", "발로란트", "overwatch", "오버워치", "maplestory", "메이플", "lost ark", "로스트아크", "epic games", "battle.net" }),
        ("win_code", new[] { "visual studio", "vs code", "rider", "intellij", "pycharm", "webstorm", "android studio", "github", "gitlab", "stack overflow", "powershell", "명령 프롬프트", "command prompt", "windows terminal", "terminal", "notepad++", "sublime text", "claude code" }),
        ("win_sheet", new[] { "excel", "엑셀", "google sheets", "sheets", "스프레드시트", ".xlsx", ".csv" }),
        ("win_docs", new[] { "microsoft word", " - word", "한글", ".hwp", "google docs", "notion", "노션", "obsidian", "onenote", "메모장", "notepad", "powerpoint", "파워포인트", ".pptx", ".docx", ".pdf" }),
        ("win_chat", new[] { "kakaotalk", "카카오톡", "discord", "디스코드", "slack", "microsoft teams", "teams", "telegram", "텔레그램", "zoom", "google meet", "webex", "whatsapp" }),
        ("win_shop", new[] { "coupang", "쿠팡", "gmarket", "지마켓", "11st", "11번가", "musinsa", "무신사", "amazon", "aliexpress", "알리익스프레스", "naver shopping", "네이버 쇼핑", "장바구니", "쇼핑" }),
        ("win_browse", new[] { "google chrome", "microsoft edge", "firefox", "whale", "웨일", "brave", "opera", "vivaldi" }),
    };
    public static string? Topic(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        string t = title.ToLowerInvariant();
        foreach (var (key, words) in Topics) foreach (var word in words) if (t.Contains(word)) return key;
        return null;
    }
}
