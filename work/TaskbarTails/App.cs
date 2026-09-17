using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;
using Forms = System.Windows.Forms;

namespace TaskbarTails;

public sealed class App : Application
{
    public PetState State = new();
    public bool Exiting, PetsVisible = true;
    public MainWindow Panel = null!;
    public RoomClient? Room;
    public Native.MonitorInfo Screen = Native.Primary();
    public int GroundY;
    RoomWindow? roomWindow;
    QuickChatWindow? quickChat;
    InfoWindow? info;
    double lastBroadcast;
    bool snapshotPending, snapshotDue;
    readonly List<PetWindow> pets = new();
    Forms.NotifyIcon? tray;
    // Render-priority timer at ~60 Hz: even pacing, and PetWindow only moves/redraws when something changed.
    readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    readonly Stopwatch clock = new();
    double last, saved, refreshed, fullscreenChecked, screenChecked, idleChecked, nightChecked, lastStretch, lastTopmost;
    bool fullscreen, napping, nightSlept, nightOverride;
    Mutex? mutex;
    static string? smokePath;
    static int smokeExitCode;
    const string ReleaseRepo = "https://github.com/bsj901022-web/TaskTic";
    UpdateManager? updater;
    UpdateInfo? pendingUpdate;
    double lastUpdateCheck = double.NegativeInfinity;
    bool updating;
    readonly Dictionary<string, double> greeted = new();
    HwndSource? hotkeySource;
    bool hotkeysRegistered;
    double? idleOverride;
    double topicChecked, lastTopicSaid = double.NegativeInfinity; string? lastTopic;
    public bool IsNapping => napping;
    public bool IsSmokeTest => smokePath != null;

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack must run first: it handles install/update/uninstall hooks and exits when invoked by Update.exe.
        VelopackApp.Build().Run();
        if (args.Length > 1 && args[0] == "--smoke-test") smokePath = Path.GetFullPath(args[1]);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception.ToString()); } catch { }
            if (smokePath == null) MessageBox.Show(L.Get("crash"), "Taskbar Tails");
            e.Handled = true; app.Shutdown(1);
        };
        int result = app.Run(); return smokeExitCode == 0 ? result : smokeExitCode;
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (smokePath == null)
        {
            mutex = new Mutex(true, "Local\\TaskbarTails.Prototype." + System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value, out bool first);
            if (!first) { MessageBox.Show(L.Get("already_running"), "Taskbar Tails"); Shutdown(); return; }
        }
        if (smokePath != null) { Directory.CreateDirectory(smokePath); StateStore.PathName = Path.Combine(smokePath, "test-state.json"); }
        Desktop.TestOnly = smokePath != null;
        State = smokePath == null ? StateStore.Load() : new PetState();
        L.Init(State.Language);
        RefreshScreen();
        Panel = new MainWindow(this); MainWindow = Panel;
        var work = Screen.Work;
        pets.Add(new PetWindow(this, State.Name, State.Species, "#FFF2DE", work.Left + (work.Right - work.Left) * .55));
        pets[0].Show(); pets[0].Say(State.HoursAway >= 8 ? L.Get("back_long") : State.HoursAway >= 1 ? L.Get("back_short") : L.Get("hello"));
        if (State.DemoFriends) SetFriends(true);
        SetupTray(); Panel.Show(); RegisterHotkeys();
        timer.Tick += (_, _) => Tick(); clock.Start(); timer.Start();
        if (smokePath == null && State.AutoRejoin && State.LastRoomCode.Length > 0) _ = AutoRejoin();
        if (smokePath != null)
        {
            var smokeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            smokeTimer.Tick += (_, _) => { smokeTimer.Stop(); RunSmoke(); }; smokeTimer.Start();
        }
    }
    public void RefreshScreen() { Screen = Native.Selected(State.MonitorIndex); GroundY = Native.GroundBottom(Screen); }
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
        tray = new Forms.NotifyIcon { Icon = icon, Text = L.Get("tray_title"), Visible = true, ContextMenuStrip = BuildTrayMenu() };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowPanel);
    }
    Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L.Get("tray_open"), null, (_, _) => Dispatcher.Invoke(ShowPanel));
        menu.Items.Add(L.Feed(PetCatalog.Get(State.Species).Group), null, (_, _) => Dispatcher.Invoke(Feed));
        menu.Items.Add(L.F("tray_say", Hotkeys.Label(State.ChatHotkey)), null, (_, _) => Dispatcher.Invoke(OpenQuickChat));
        menu.Items.Add(L.F("tray_toggle_key", Hotkeys.Label(State.HideHotkey)), null, (_, _) => Dispatcher.Invoke(ToggleVisible));
        menu.Items.Add(L.F(State.AutoBubbles ? "tray_bubbles_off" : "tray_bubbles_on", Hotkeys.Label(State.BubbleHotkey)), null, (_, _) => Dispatcher.Invoke(ToggleBubbles));
        menu.Items.Add(L.Get("update_check"), null, (_, _) => Dispatcher.Invoke(() => { ShowPanel(); _ = CheckForUpdates(true); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(L.Get("quit"), null, (_, _) => Dispatcher.Invoke(Quit));
        return menu;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Min(.1, now - last); last = now;
        State.Tick(dt);
        if (now - screenChecked > 2) { RefreshScreen(); screenChecked = now; }
        if (State.WindowPlay && pets.Count > 0) WindowCheck(now);
        if (smokePath == null && now - fullscreenChecked > 1) { fullscreen = Native.FullscreenApp(Screen); fullscreenChecked = now; SyncVisibility(); }
        if ((smokePath == null || idleOverride != null) && now - idleChecked > 1) { idleChecked = now; IdleCheck(); }
        if (smokePath == null && now - nightChecked > 30) { nightChecked = now; NightCheck(DateTime.Now.Hour); }
        if (smokePath == null && State.StretchReminder && now - lastStretch > 3000) { lastStretch = now; StretchNow(); }
        foreach (var pet in pets.ToArray()) if (pet.IsVisible) pet.Step(dt);
        if (now - lastTopmost > 4) { lastTopmost = now; foreach (var p in pets) if (p.IsVisible) p.Place(true); }
        // Friends simulate my character themselves; only sleep changes (within 0.3 s) and a 15 s presence heartbeat go out as "state".
        if (Room?.Connected == true && !snapshotPending && ((snapshotDue && now - lastBroadcast > .3) || now - lastBroadcast > 15)) { lastBroadcast = now; snapshotDue = false; Broadcast("state"); }
        Panel.Animate(now);
        if (now - refreshed > .5) { Panel.Refresh(); refreshed = now; }
        if (now - saved > 15) { StateStore.Save(State); saved = now; }
        if (smokePath == null && now - lastUpdateCheck > (lastUpdateCheck < 0 ? 8 : 6 * 3600)) { lastUpdateCheck = now; _ = CheckForUpdates(false); }
    }
    // --- windows as platforms ---
    // One window scan only as often as the situation needs: 4 Hz while a character is airborne or on a window, otherwise every 2 s.
    // The foreground title is polled (title only, no enumeration) every 2 s for a one-time reaction when the user switches to something new.
    void WindowCheck(double now)
    {
        bool active = pets.Any(p => !p.IsRemote && (p.IsFalling || p.IsPerched || p.IsRising));
        Desktop.Scan(Screen, now, active ? .25 : 2);
        if (now - topicChecked < 2) return; topicChecked = now;
        var topic = Desktop.Topic(Desktop.ForegroundTitle());
        if (topic == lastTopic) return; lastTopic = topic;
        if (topic != null && now - lastTopicSaid > 90 && !State.Sleeping && State.AutoBubbles && !pets[0].IsBusy) { lastTopicSaid = now; pets[0].LookAtViewer(5, L.Get(topic)); }
    }
    // --- presence: idle nap, night sleep, stretch reminder ---
    void IdleCheck()
    {
        if (State.IdleMinutes <= 0) { napping = false; return; }
        double idle = idleOverride ?? Native.IdleSeconds();
        if (!napping && !State.Sleeping && idle > State.IdleMinutes * 60) { napping = true; State.Sleeping = true; pets[0].Say(L.Get("idle_nap")); Panel.Refresh(); Broadcast("state"); }
        else if (napping && idle < 3) { napping = false; if (State.Sleeping) { State.Sleeping = false; pets[0].Say(L.Get("welcome_back")); pets[0].Bounce(); Panel.Refresh(); Broadcast("state"); } }
    }
    public void SimulateIdle(double? seconds) { idleOverride = seconds; if (seconds != null) IdleCheck(); }
    public static bool IsNight(int hour) => hour >= 23 || hour < 7;
    // Smoke test helper: the real clock may be inside the night window, which would otherwise carry a manual-wake override into the check.
    public void ResetNightState() { nightSlept = false; nightOverride = false; napping = false; }
    public void NightCheck(int hour)
    {
        if (!State.NightSleep) { nightSlept = false; nightOverride = false; return; }
        if (IsNight(hour)) { if (!nightSlept && !nightOverride && !State.Sleeping) { State.Sleeping = true; nightSlept = true; pets[0].Say(L.Get("night_sleep")); Panel.Refresh(); } }
        else { if (nightSlept && State.Sleeping && !napping) { State.Sleeping = false; pets[0].Say(L.Get("morning_wake")); Panel.Refresh(); } nightSlept = false; nightOverride = false; }
    }
    public void StretchNow()
    {
        if (State.Sleeping || pets.Count == 0) return;
        var kind = PetCatalog.Get(State.Species); pets[0].Act(kind.SecondAction); pets[0].Say(L.Get("stretch"));
    }
    // --- friends: greeting, poke, ball ---
    // Once per session, when a friend's character appears in the room, both say hi. Characters live at different spots on
    // each PC now, so "meeting" on screen means nothing and no longer triggers greetings.
    public void GreetJoin(PetWindow newcomer)
    {
        if (!State.GreetFriends || pets.Count == 0 || !newcomer.IsRemote || greeted.ContainsKey(newcomer.RemoteId)) return;
        greeted[newcomer.RemoteId] = clock.Elapsed.TotalSeconds;
        pets[0].Greet(newcomer.PetName, newcomer.CenterX); newcomer.Greet(State.Name, pets[0].CenterX);
    }
    public void Poke(PetWindow remote)
    {
        remote.Bounce(); remote.Say(L.Get("poke_local")); Sounds.Pop(State.ClickSound);
        Broadcast("poke", "", remote.RemoteId);
    }
    public void PokeMember(string userId, string name)
    {
        var remote = pets.FirstOrDefault(p => p.IsRemote && p.RemoteId == userId);
        if (remote != null) { Poke(remote); return; }
        pets[0].Say(L.Get("poke_local")); Broadcast("poke", "", userId);
    }
    public void ThrowBallTo(PetWindow remote)
    {
        bool toRight = remote.CenterX >= pets[0].CenterX;
        pets[0].ThrowBall(toRight); pets[0].Say(L.Get("ball_thrown"));
        Broadcast("ball", "", remote.RemoteId, !toRight);
    }
    public void ThrowBallToMember(string userId)
    {
        var remote = pets.FirstOrDefault(p => p.IsRemote && p.RemoteId == userId);
        if (remote != null) { ThrowBallTo(remote); return; }
        pets[0].ThrowBall(true); pets[0].Say(L.Get("ball_thrown")); Broadcast("ball", "", userId, false);
    }
    // --- quick bubble, hotkeys, startup, language ---
    public void OpenQuickChat()
    {
        if (pets.Count == 0) return;
        quickChat ??= new QuickChatWindow(this);
        pets[0].LookAtViewer(10);
        var (x, top) = pets[0].HeadPoint();
        quickChat.ShowAt(x, top, Screen);
    }
    // A short line that fits the moment: hunger first, then time of day, otherwise small talk.
    readonly Random chatterRandom = new();
    public string Chatter()
    {
        if (State.Fullness < 30) return L.Get("chat_hungry");
        if (State.WindowPlay && chatterRandom.Next(2) == 0) { var topic = Desktop.Topic(Desktop.ForegroundTitle()); if (topic != null) return L.Get(topic); }
        int hour = DateTime.Now.Hour;
        if (chatterRandom.Next(3) == 0)
        {
            if (hour is >= 6 and < 11) return L.Get("chat_morning");
            if (hour is >= 11 and < 14) return L.Get("chat_lunch");
            if (hour is >= 21 or < 6) return L.Get("chat_night");
        }
        return L.Get("chat_" + chatterRandom.Next(8));
    }
    // About · Contact side panel docked to the main window.
    public void ShowInfo() { info ??= new InfoWindow(this, Panel); info.ShowDocked(); }
    public void HideInfo() => info?.Hide();
    public bool InfoVisible => info?.IsVisible == true;
    public bool InfoDocked => info?.IsDockedBeside == true;
    public int InfoLinkCount => info?.LinkCount ?? 0;
    public bool QuickChatVisible => quickChat?.IsVisible == true;
    public void HideQuickChat() => quickChat?.Hide();
    public void RegisterHotkeys()
    {
        UnregisterHotkeys();
        if (!State.HotkeysEnabled || smokePath != null) return;
        var handle = new WindowInteropHelper(Panel).Handle; if (handle == IntPtr.Zero) return;
        hotkeySource = HwndSource.FromHwnd(handle); hotkeySource?.AddHook(HotkeyHook);
        bool ok = true;
        foreach (var (id, combo) in new[] { (1, State.HideHotkey), (2, State.ChatHotkey), (3, State.BubbleHotkey) }) { var (mods, key) = Native.ParseHotkey(combo); ok &= key != 0 && Native.RegisterHotKey(handle, id, mods, key); }
        hotkeysRegistered = true;
        if (!ok) Panel.Notice.Text = L.Get("hotkey_failed");
    }
    void UnregisterHotkeys()
    {
        if (!hotkeysRegistered) return;
        var handle = new WindowInteropHelper(Panel).Handle;
        if (handle != IntPtr.Zero) { Native.UnregisterHotKey(handle, 1); Native.UnregisterHotKey(handle, 2); Native.UnregisterHotKey(handle, 3); }
        hotkeySource?.RemoveHook(HotkeyHook); hotkeySource = null; hotkeysRegistered = false;
    }
    IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg != 0x0312) return IntPtr.Zero;
        if (w.ToInt32() == 1) ToggleVisible(); else if (w.ToInt32() == 2) OpenQuickChat(); else if (w.ToInt32() == 3) ToggleBubbles();
        handled = true; return IntPtr.Zero;
    }
    // Opt-in only (settings checkbox): HKCU Run key pointing at this exe.
    public void ApplyStartup(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable) key.SetValue("TaskbarTails", "\"" + (Environment.ProcessPath ?? "") + "\""); else key.DeleteValue("TaskbarTails", false);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { Panel.Notice.Text = L.F("startup_failed", e.Message); }
    }
    // Rebuilds every window and menu in the new language; the characters and the room connection stay as they are.
    public void SetLanguage(string lang)
    {
        State.Language = lang; L.Init(lang); StateStore.Save(State);
        UnregisterHotkeys();
        var old = Panel; bool wasVisible = old.IsVisible;
        Panel = new MainWindow(this); MainWindow = Panel; if (wasVisible) Panel.Show();
        old.AllowClose = true; old.Close();
        if (roomWindow != null) { var r = roomWindow; roomWindow = null; r.AllowClose = true; r.Close(); }
        if (quickChat != null) { var q = quickChat; quickChat = null; q.AllowClose = true; q.Close(); }
        if (info != null) { var i = info; info = null; i.AllowClose = true; i.Close(); }
        foreach (var p in pets) p.BuildMenu();
        if (tray != null) { tray.Text = L.Get("tray_title"); var previous = tray.ContextMenuStrip; tray.ContextMenuStrip = BuildTrayMenu(); previous?.Dispose(); }
        RegisterHotkeys(); Panel.Refresh();
    }
    public void RefreshTrayMenu() { if (tray == null) return; var previous = tray.ContextMenuStrip; tray.ContextMenuStrip = BuildTrayMenu(); previous?.Dispose(); }
    public string VersionLabel => "v" + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
    // Installed builds check GitHub Releases; the portable folder build only reports that it is portable.
    public async Task CheckForUpdates(bool manual)
    {
        if (updating) return; updating = true;
        try
        {
            updater ??= new UpdateManager(new GithubSource(ReleaseRepo, null, false));
            if (!updater.IsInstalled) { if (manual) Panel.Notice.Text = L.Get("portable_no_update"); return; }
            if (pendingUpdate != null) { Panel.ShowUpdateReady(pendingUpdate.TargetFullRelease.Version.ToString()); return; }
            var info = await updater.CheckForUpdatesAsync();
            if (info == null) { if (manual) Panel.Notice.Text = L.F("latest_version", VersionLabel); return; }
            string target = info.TargetFullRelease.Version.ToString();
            Panel.Notice.Text = L.F("downloading", target);
            await updater.DownloadUpdatesAsync(info, p => Dispatcher.BeginInvoke(new Action(() => Panel.Notice.Text = L.F("downloading_pct", target, p))));
            pendingUpdate = info; Panel.ShowUpdateReady(target); pets[0].Say(L.F("update_ready_bubble", target));
        }
        catch (Exception e) { if (manual) Panel.Notice.Text = L.F("update_failed", e.Message); }
        finally { updating = false; }
    }
    public void ApplyUpdate()
    {
        if (pendingUpdate == null || updater == null) return;
        StateStore.Save(State); Exiting = true; timer.Stop(); Room?.Dispose();
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        updater.ApplyUpdatesAndRestart(pendingUpdate);
    }
    public void ShowPanel() { Panel.Show(); Panel.WindowState = WindowState.Normal; Panel.Activate(); }
    public void RequestSnapshot() => snapshotDue = true;
    public void Changed(string message) { StateStore.Save(State); pets[0].Say(message); Panel.Refresh(); Broadcast("message", message, auto: true); }
    // Hotkey / tray / settings: automatic bubbles on or off. Typed messages are never affected.
    public void ToggleBubbles() => SetBubbles(!State.AutoBubbles);
    public void SetBubbles(bool on)
    {
        State.AutoBubbles = on; StateStore.Save(State);
        if (!on) foreach (var p in pets) p.Visual.Bubble = "";
        else if (pets.Count > 0) pets[0].Say(L.Get("bubbles_on"));
        Panel.Notice.Text = on ? L.Get("bubbles_on_notice") : L.F("bubbles_off_notice", Hotkeys.Label(State.BubbleHotkey));
        Panel.Refresh(); RefreshTrayMenu();
    }
    public void Feed()
    {
        var kind = PetCatalog.Get(State.Species);
        if (State.Feed()) { Changed(L.Get(kind.Group == "사람" ? "yum_human" : "yum")); return; }
        // Too full: nothing eaten, a head shake and a small XP/happiness penalty (see PetState.Feed).
        pets[0].Refuse(); Changed(L.Get("too_full_" + chatterRandom.Next(3)));
        Panel.Notice.Text = L.F("overfed_notice", 4, PetState.FullThreshold);
    }
    // Menus show "먹이 주기" or "음식 먹기" depending on the character kind.
    public void RefreshMenus() { RefreshTrayMenu(); foreach (var p in pets) p.BuildMenu(); }
    public void Pet() { State.Pet(); Sounds.Pop(State.ClickSound); var kind = PetCatalog.Get(State.Species); Changed(L.Touch(kind.Id, kind.Group)); }
    public void Play() { State.Play(); pets[0].Act(PetCatalog.Get(State.Species).FirstAction); Changed(L.Get("lets_play")); }
    public void ToggleSleep()
    {
        State.Sleeping = !State.Sleeping; napping = false;
        if (!State.Sleeping && IsNight(DateTime.Now.Hour)) nightOverride = true;
        Changed(State.Sleeping ? L.Get("good_night") : L.Get("good_morning"));
    }
    public void ToggleVisible() { PetsVisible = !PetsVisible; SyncVisibility(); Panel.Refresh(); }
    void SyncVisibility() { foreach (var p in pets) { bool show = PetsVisible && !fullscreen; if (show && !p.IsVisible) p.Show(); else if (!show && p.IsVisible) p.Hide(); } }
    public void SetFriends(bool enabled)
    {
        State.DemoFriends = enabled;
        if (enabled && !pets.Any(p => p.IsDemo))
        {
            var w = Screen.Work; var span = w.Right - w.Left;
            pets.Add(new PetWindow(this, L.Get("demo_bori"), "dog", "#F3C7AF", w.Left + span * .3, true));
            pets.Add(new PetWindow(this, L.Get("demo_cloud"), "rabbit", "#E7E4F5", w.Left + span * .75, true));
        }
        else if (!enabled) { foreach (var p in pets.Where(p => p.IsDemo).ToArray()) { p.Close(); pets.Remove(p); } }
        SyncVisibility(); StateStore.Save(State);
    }
    public void Specialty(int index)
    {
        State.Sleeping = false; var kind = PetCatalog.Get(State.Species);
        pets[0].Act(index == 0 ? kind.FirstAction : kind.SecondAction);
        Changed(index == 0 ? PetCatalog.FirstLabel(kind) : PetCatalog.SecondLabel(kind));
    }
    public void SendBubble(string text)
    {
        text = text.Trim(); if (text.Length == 0) return; if (text.Length > 80) text = text[..80];
        pets[0].Say(text, false); Broadcast("message", text);
    }
    // --- rooms ---
    public void ShowRoom()
    {
        if (roomWindow == null) { roomWindow = new RoomWindow(this) { Owner = Panel }; roomWindow.Closed += (_, _) => roomWindow = null; }
        roomWindow.Show(); roomWindow.Activate();
    }
    public void EnsureRoom()
    {
        if (Room != null) return;
        Room = new RoomClient();
        Room.CurrentPet = () => Dispatcher.Invoke(() => pets[0].Snapshot());
        Room.Status += text => Dispatcher.BeginInvoke(new Action(() => { roomWindow?.UpdateStatus(text); Panel.Notice.Text = text; }));
        Room.Received += ev => Dispatcher.BeginInvoke(new Action(() => HandleEvent(ev, Room.UserId)));
        Room.RosterChanged += members => Dispatcher.BeginInvoke(new Action(() => {
            roomWindow?.UpdateRoster(members);
            foreach (var p in pets.Where(p => p.IsRemote && !members.Any(m => m.UserId == p.RemoteId)).ToArray()) { p.Close(); pets.Remove(p); }
            foreach (var m in members.Where(m => m.UserId != Room.UserId))
            {
                if (pets.Any(p => p.IsRemote && p.RemoteId == m.UserId)) continue;
                var w = Screen.Work; var p = new PetWindow(this, m.Name, m.Species, "", w.Left + (w.Right - w.Left) * .4, false, true) { RemoteId = m.UserId }; pets.Add(p); SyncVisibility(); GreetJoin(p);
            }
            Panel.Refresh(); Broadcast("state");
        }));
    }
    // Create (create=true, value=name) or join/switch by invite code; remembers the code for auto-rejoin.
    public async Task JoinRoom(string value, bool create)
    {
        EnsureRoom();
        await Room!.Open(value, create, State);
        State.LastRoomCode = Room.InviteCode; StateStore.Save(State);
        Panel.Refresh(); Broadcast("state");
    }
    public void ForgetRoom() { State.LastRoomCode = ""; StateStore.Save(State); Panel.Refresh(); }
    async Task AutoRejoin()
    {
        try { await JoinRoom(State.LastRoomCode, false); }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or InvalidOperationException or TaskCanceledException or TimeoutException) { Panel.Notice.Text = L.F("rc_reconnect", e.Message); }
    }
    // One incoming realtime event. myId is this user's id (a fixed value in the smoke test).
    public void HandleEvent(PetEvent ev, string myId)
    {
        if (Exiting || pets.Count == 0) return;
        var pet = pets.FirstOrDefault(p => p.IsRemote && p.RemoteId == ev.UserId);
        if (pet == null) { var w = Screen.Work; pet = new PetWindow(this, ev.Name, ev.Species, "", w.Left + (w.Right - w.Left) * ev.X, false, true) { RemoteId = ev.UserId }; pets.Add(pet); SyncVisibility(); GreetJoin(pet); _ = RefreshRosterSafe(); }
        double now = clock.Elapsed.TotalSeconds;
        if (ev.Kind == "poke" && ev.Target == myId) { pets[0].Bounce(); pets[0].Say(L.F("poked_by", ev.Name)); State.Happiness = Math.Min(100, State.Happiness + 2); Sounds.Pop(State.ClickSound); }
        else if (ev.Kind == "greet" && ev.Target == myId) { greeted[ev.UserId] = now; pets[0].Greet(ev.Name, pet.CenterX); pet.Greet(State.Name, pets[0].CenterX); }
        else if (ev.Kind == "ball" && ev.Target == myId) { pet.Bounce(); pets[0].ReceiveBall(!ev.Left, ev.Name); }
        pet.Apply(ev);
    }
    // A newcomer is visible in the roster right away instead of waiting for the 15s heartbeat.
    async Task RefreshRosterSafe() { try { if (Room != null) await Room.RefreshRoster(); } catch (Exception e) when (e is System.Net.Http.HttpRequestException or InvalidOperationException or TaskCanceledException) { Panel.Notice.Text = L.F("rc_roster_wait", e.Message); } }
    public string RoomSummary => Room?.Connected == true ? L.F("room_summary", Room.RoomName, Room.InviteCode, Math.Max(1, Room.Members.Count)) : "";
    public async void Broadcast(string kind, string message = "", string target = "", bool? left = null, bool auto = false)
    {
        if (Room?.Connected != true || pets.Count == 0) return;
        if (kind == "state" && snapshotPending) return;
        bool snapshot = kind == "state"; if (snapshot) snapshotPending = true;
        try { var ev = pets[0].Snapshot(kind, message); ev.Target = target; ev.Auto = auto; if (left != null) ev.Left = left.Value; await Room.Publish(ev); }
        catch (Exception e) { Panel.Notice.Text = L.F("send_wait", e.Message); }
        finally { if (snapshot) snapshotPending = false; }
    }
    public void Quit()
    {
        if (Exiting) return;
        Exiting = true; timer.Stop(); UnregisterHotkeys(); Room?.Dispose(); roomWindow?.Close(); quickChat?.Close(); info?.Close(); StateStore.Save(State);
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
            Check(pets[0].InsideWorkArea(), "overlay aligned with the selected work-area bottom");
            var exp = State.Experience; Feed(); Check(State.Experience == exp + 5, "feed increases XP");
            State.Fullness = 84; Feed(); Check(State.Fullness == 100, "fullness capped at 100");
            Play(); Check(!State.Sleeping, "play wakes pet");
            ToggleSleep(); Check(State.Sleeping, "sleep toggles on"); ToggleSleep();
            SetFriends(true); Check(pets.Count == 3, "two local demo friends added");
            SetFriends(true); Check(pets.Count == 3, "demo toggle is idempotent");
            ToggleVisible(); Check(pets.All(p => !p.IsVisible), "hide all overlays");
            ToggleVisible(); Check(pets.All(p => p.IsVisible), "show all overlays");
            State.Species = "rabbit"; pets[0].Step(.033); Check(pets[0].Visual.Species == "rabbit", "species applied to overlay");
            foreach (var kind in PetCatalog.All)
            {
                var set = SpriteLibrary.Get(kind.Id); Check(set != null, "sprites load for " + kind.Id);
                Check(set!.WalkFrames("east") == 16 && set.WalkFrames("west") == 16, "16-frame walk east/west for " + kind.Id);
                Check(set.HasAction(kind.FirstAction) && set.HasAction(kind.SecondAction), "special motions " + kind.FirstAction + "/" + kind.SecondAction + " for " + kind.Id);
                State.Species = kind.Id; pets[0].Step(.033); pets[0].Act(kind.FirstAction, false); pets[0].Step(.033);
                Check(kind.FirstAction == "fetch" ? pets[0].IsFetching : pets[0].Visual.ActionKey == kind.FirstAction, "motion starts for " + kind.Id);
            }
            State.Species = "cat"; pets[0].Act("loaf", false); State.Species = "rabbit"; pets[0].Step(.033);
            Check(pets[0].Visual.ActionKey == "" && !pets[0].IsFetching, "species change resets the running motion");
            State.Species = "dog"; pets[0].Step(.033); pets[0].Act("fetch", false);
            for (int i = 0; i < 900 && pets[0].IsFetching; i++) pets[0].Step(.033);
            Check(!pets[0].IsFetching && pets[0].Visual.ActionKey == "wag", "dog throws, fetches and returns the ball");
            State.Species = "cat"; pets[0].Step(.033);
            pets[0].TestDrop(.5); pets[0].Step(.2); Check(pets[0].IsFalling && pets[0].Visual.Parachute, "parachute opens after drop");
            pets[0].TestInterruptFall(); pets[0].Step(.033); Check(pets[0].IsFalling, "a click while airborne resumes the fall instead of hovering");
            for (int i = 0; i < 900 && pets[0].IsFalling; i++) pets[0].Step(.033);
            Check(!pets[0].IsFalling && !pets[0].Visual.Parachute && pets[0].InsideWorkArea(), "parachute lands on the work area");
            pets[0].Say(new string('가', 80)); pets[0].Step(.033); pets[0].UpdateLayout(); Check(pets[0].Visual.Bubble.Length == 80, "80-character bubble renders");
            State.Sleeping = true; pets[0].Step(.033); Check(pets[0].Visual.ActionKey == "loaf", "sleeping cat rests in loaf pose"); State.Sleeping = false; pets[0].Step(.033);
            foreach (var size in new[] { 150, 200 }) { State.Scale = size; pets[0].Say("크기 " + size); pets[0].Step(.033); pets[0].UpdateLayout(); Check(Math.Abs(pets[0].Visual.SizeFactor - size / 100.0) < .001 && pets[0].InsideWorkArea(), size + "% size applies and keeps the feet on the work area"); }
            State.Scale = 100; pets[0].Step(.033); StateStore.Save(State); Check(StateStore.Load().Scale == 100, "size setting persists");
            var remote = new PetWindow(this, "원격", "fox", "", 200, false, true) { RemoteId = "remote-test" }; remote.Show();
            remote.Apply(new PetEvent { Kind = "parachute", UserId = "remote-test", Name = "원격", Species = "fox", X = .5, Lift = .5 }); for (int i = 0; i < 6; i++) remote.Step(.033);
            Check(remote.Visual.Parachute, "remote parachute stays visible between snapshots");
            remote.Apply(new PetEvent { Kind = "land", UserId = "remote-test", Name = "원격", Species = "fox", X = .5, Lift = 0 }); for (int i = 0; i < 300; i++) remote.Step(.033);
            Check(!remote.Visual.Parachute, "remote parachute closes on land"); remote.Close();
            // --- v0.6 ---
            Check(Native.Monitors().Count >= 1 && GroundY > Screen.Work.Top, "monitor enumeration and ground line");
            Check(IsNight(23) && IsNight(3) && !IsNight(7) && !IsNight(12), "night hours");
            var away = new PetState { Fullness = 80, Happiness = 80, LastSeenUtc = DateTime.UtcNow.AddHours(-10) }; away.ApplyOfflineTime(DateTime.UtcNow);
            Check(Math.Abs(away.Fullness - 60) < .05 && Math.Abs(away.Happiness - 70) < .05 && away.HoursAway > 9.9, "offline hours lower fullness and happiness");
            var longAway = new PetState { Fullness = 30, Happiness = 35, LastSeenUtc = DateTime.UtcNow.AddHours(-100) }; longAway.ApplyOfflineTime(DateTime.UtcNow);
            Check(longAway.Fullness == 20 && longAway.Happiness == 30, "offline decay stops at the friendly floor");
            State.Sleeping = false; State.IdleMinutes = 5; SimulateIdle(600); Check(State.Sleeping && IsNapping, "idle for 10 minutes starts a nap");
            SimulateIdle(0); Check(!State.Sleeping && !IsNapping, "input after a nap wakes the pet"); SimulateIdle(null);
            ResetNightState(); State.NightSleep = true; NightCheck(23); Check(State.Sleeping, "night check puts the pet to sleep at 23:00"); NightCheck(8); Check(!State.Sleeping, "morning check wakes the pet");
            StretchNow(); Check(pets[0].Visual.Bubble == L.Get("stretch"), "stretch reminder fires a bubble");
            var friend = new PetWindow(this, "친구", "penguin", "", pets[0].CenterX, false, true) { RemoteId = "friend-1" }; friend.Show(); pets.Add(friend); friend.Step(.033);
            pets[0].Visual.Bubble = ""; GreetJoin(friend); Check(pets[0].Visual.Bubble == L.F("greet", "친구") && friend.Visual.Bubble == L.F("greet", State.Name), "characters greet once when a friend joins");
            pets[0].Visual.Bubble = ""; GreetJoin(friend); Check(pets[0].Visual.Bubble == "", "no repeated greeting for the same friend");
            HandleEvent(new PetEvent { Kind = "poke", UserId = "friend-1", Target = "me", Name = "친구", Species = "penguin", X = .3 }, "me"); Check(pets[0].Visual.Bubble == L.F("poked_by", "친구"), "poke from a friend shows on my character");
            HandleEvent(new PetEvent { Kind = "ball", UserId = "friend-1", Target = "me", Name = "친구", Species = "penguin", X = .3, Left = false }, "me"); Check(pets[0].IsBallMoving, "incoming ball starts rolling");
            for (int i = 0; i < 200 && pets[0].IsBallMoving; i++) pets[0].Step(.033); Check(!pets[0].IsBallMoving && pets[0].Visual.Bubble == L.Get("ball_caught"), "incoming ball is caught");
            pets[0].ThrowBall(true); for (int i = 0; i < 200 && pets[0].IsBallMoving; i++) pets[0].Step(.033); Check(!pets[0].IsBallMoving, "outgoing ball leaves the screen");
            pets.Remove(friend); friend.Close();
            pets[0].Visual.Bubble = ""; pets[0].LookAtViewer(5, Chatter()); pets[0].Step(.033);
            Check(pets[0].IsFacingViewer && !pets[0].Visual.Walking && pets[0].Visual.Bubble.Length > 0, "character turns to the viewer and says a line");
            for (int i = 0; i < 200 && pets[0].IsFacingViewer; i++) pets[0].Step(.033); Check(!pets[0].IsFacingViewer, "character turns back after a few seconds");
            Check(PetVisual.DevicePixelsPerSprite(1) == 1 && PetVisual.DevicePixelsPerSprite(1.5) == 1.5 && PetVisual.DevicePixelsPerSprite(2) == 2 && PetVisual.DevicePixelsPerSprite(.5) == 1, "sprites are drawn at whole or half device pixels");
            ShowInfo(); Panel.UpdateLayout(); Check(InfoVisible && InfoLinkCount >= 8 && InfoDocked, "info panel docks beside the main window with contact links"); HideInfo(); Check(!InfoVisible, "info panel hides on close");
            Check(!PetCatalog.Selectable.Any(k => PetCatalog.Hidden.Contains(k.Id)) && PetCatalog.Selectable.Any(k => k.Id == "robot") && PetCatalog.Valid("slime"), "hidden creature kinds stay valid but are not selectable");
            State.NameStyle = 2; pets[0].Step(.033); pets[0].UpdateLayout(); Check(pets[0].Visual.NameStyle == 2 && pets[0].Visual.ShowName, "large name label applies");
            State.NameStyle = 0; pets[0].Step(.033); Check(!pets[0].Visual.ShowName, "name can be hidden"); State.NameStyle = 1; pets[0].Step(.033);
            { var seen = new HashSet<string>(); for (int i = 0; i < 12; i++) seen.Add(L.Touch("cat", "동물")); Check(seen.Count >= 3 && seen.Contains(L.Get("touch_cat_0")) || seen.Count >= 3, "touch reactions vary per character"); }
            // --- v0.6.4: feeding limits and windows as platforms ---
            Check(L.Feed("사람") == L.Get("feed_human") && L.Feed("동물") == L.Get("feed") && L.Feed("사람") != L.Feed("동물"), "feed label depends on the character group");
            State.Fullness = 90; var xpFull = State.Experience; Feed();
            double shakeMax = 0; for (int i = 0; i < 6; i++) { pets[0].Step(.033); shakeMax = Math.Max(shakeMax, Math.Abs(pets[0].Visual.Shake)); }
            Check(State.Fullness == 90 && State.Experience == Math.Max((State.Level - 1) * 100, xpFull - 4) && pets[0].IsRefusing && pets[0].IsFacingViewer && shakeMax > .5, "overfeeding is refused, costs XP and shakes the character");
            for (int i = 0; i < 90 && pets[0].IsRefusing; i++) pets[0].Step(.033); Check(!pets[0].IsRefusing && Math.Abs(pets[0].Visual.Shake) < .001, "refusal motion ends");
            State.Fullness = 40; xpFull = State.Experience; Feed(); Check(State.Fullness == 58 && State.Experience == xpFull + 5, "feeding below the limit works again");
            Check(Desktop.Topic("(3) YouTube - Google Chrome") == "win_video" && Desktop.Topic("App.cs - TaskbarTails - Visual Studio Code") == "win_code" && Desktop.Topic("Untitled") == null, "foreground window topics");
            State.WindowPlay = true; int platformTop = GroundY - 320, platformLeft = Screen.Work.Left + 200, platformRight = platformLeft + 500;
            Desktop.TestPlatform(new Native.Rect { Left = platformLeft, Top = platformTop, Right = platformRight, Bottom = GroundY }); Desktop.Refresh(Screen);
            pets[0].TestMoveTo(platformLeft + 250 - pets[0].Width * pets[0].Dpi / 2); pets[0].TestDrop(.8);
            for (int i = 0; i < 900 && pets[0].IsFalling; i++) pets[0].Step(.033);
            Check(pets[0].IsPerched && Math.Abs(pets[0].FeetY - platformTop) < 1, "dropped character lands on a window's top edge");
            double beforeX = pets[0].CenterX; Desktop.TestPlatform(new Native.Rect { Left = platformLeft + 120, Top = platformTop - 40, Right = platformRight + 120, Bottom = GroundY }); Desktop.Refresh(Screen); pets[0].Step(.033);
            Check(pets[0].IsPerched && Math.Abs(pets[0].CenterX - beforeX - 120) < 1 && Math.Abs(pets[0].FeetY - (platformTop - 40)) < 1, "perched character follows the window when it moves");
            Desktop.TestPlatform(null); Desktop.Refresh(Screen); pets[0].Step(.033); Check(!pets[0].IsPerched && pets[0].IsFalling, "character drops when the window disappears");
            for (int i = 0; i < 900 && pets[0].IsFalling; i++) pets[0].Step(.033); Check(!pets[0].IsFalling && pets[0].InsideWorkArea(), "falls back to the taskbar");
            int wallLeft = (int)pets[0].CenterX + 260; Desktop.TestPlatform(new Native.Rect { Left = wallLeft, Top = GroundY - 420, Right = wallLeft + 500, Bottom = GroundY }); Desktop.Refresh(Screen);
            Check(pets[0].TestRise(), "a window top edge nearby is chosen for a balloon ride");
            bool floated = false; for (int i = 0; i < 1500 && !pets[0].IsPerched; i++) { pets[0].Step(.033); floated |= pets[0].IsRising && pets[0].Visual.Balloon; }
            Check(pets[0].IsPerched && floated && Math.Abs(pets[0].FeetY - (GroundY - 420)) < 1 && !pets[0].Visual.Balloon, "character walks under the edge, floats up with a balloon and stands on top");
            Desktop.TestPlatform(null); Desktop.Refresh(Screen); for (int i = 0; i < 900 && (pets[0].IsPerched || pets[0].IsFalling); i++) pets[0].Step(.033); Check(pets[0].InsideWorkArea(), "back on the ground after the test window closes");
            // --- v0.6.7: screen edges ---
            State.EdgeRoam = true; pets[0].TestStartWall(1); pets[0].Step(.033); var edgeRect = pets[0].WindowRect();
            Check(pets[0].Surface == 1 && Math.Abs(edgeRect.Left + 110 * pets[0].Dpi - Screen.Work.Left) <= 1 && pets[0].Visual.Surface == 1, "character steps onto the left screen edge with its feet on the edge");
            for (int i = 0; i < 40; i++) pets[0].Step(.033); pets[0].Say("벽 타기!", false); pets[0].Visual.InvalidateVisual(); pets[0].UpdateLayout(); Export(pets[0], Path.Combine(smokePath!, "pet-wall.png"));
            for (int i = 0; i < 4000 && pets[0].Surface == 1; i++) pets[0].Step(.033);
            Check(pets[0].Surface == 3 && Math.Abs(pets[0].FeetY - Screen.Work.Top) < 1, "climbs the left edge to the top and turns onto the ceiling");
            for (int i = 0; i < 40; i++) pets[0].Step(.033); pets[0].Say("천장 산책 중"); pets[0].Visual.InvalidateVisual(); pets[0].UpdateLayout(); Export(pets[0], Path.Combine(smokePath!, "pet-ceiling.png"));
            for (int i = 0; i < 8000 && pets[0].Surface == 3; i++) pets[0].Step(.033);
            Check(pets[0].Surface == 2, "walks the ceiling to the right edge and starts down");
            for (int i = 0; i < 4000 && pets[0].Surface == 2; i++) pets[0].Step(.033);
            Check(pets[0].Surface == 0 && !pets[0].IsFalling && pets[0].InsideWorkArea(), "comes down the right edge back onto the taskbar");
            pets[0].TestRequestWall(); for (int i = 0; i < 4000 && pets[0].Surface == 0; i++) pets[0].Step(.033);
            Check(pets[0].Surface is 1 or 2 && pets[0].Visual.Walking, "menu command walks to the nearest edge and climbs it");
            pets[0].TestDrop(0); pets[0].Step(.033); Check(pets[0].Surface == 0 && !pets[0].IsFalling, "test drop returns the character to the ground");
            SendBubble("❤️"); Check(pets[0].Visual.Bubble == "❤️", "quick reaction shows as a bubble");
            OpenQuickChat(); Check(QuickChatVisible, "quick chat opens above the character"); HideQuickChat();
            Check(PetState.UnlockLevel(3) == 8 && new PetState { BubbleStyle = 3 }.EffectiveBubbleStyle == 0 && new PetState { BubbleStyle = 3, Experience = 800 }.EffectiveBubbleStyle == 3, "bubble styles unlock by level");
            SetLanguage("en"); Check(L.Get("feed") == "Feed" && Panel.Title.Contains("A day") && Panel.IsVisible, "english ui rebuilds the panel");
            SetLanguage("ko"); Check(L.Get("feed") == "먹이 주기" && Panel.Title.Contains("작은 친구들"), "korean ui restored");
            Check(Hotkeys.Compose(System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt, System.Windows.Input.Key.T) == "ctrl+alt+t" && Hotkeys.IsValid("ctrl+alt+t") && Hotkeys.IsValid("f9") && !Hotkeys.IsValid("t") && Hotkeys.Conflicts("ctrl+t") && !Hotkeys.Conflicts("ctrl+alt+t") && Hotkeys.Label("ctrl+alt+t") == "Ctrl+Alt+T", "hotkey capture, validation and conflict flags");
            Panel.ApplyHotkey("ctrl+t"); Check(State.ChatHotkey == "ctrl+t" && Panel.HotkeyNote.Visibility == Visibility.Visible, "custom hotkey saved with a conflict warning"); Panel.ApplyHotkey(Hotkeys.Default); Check(Panel.HotkeyNote.Visibility == Visibility.Collapsed, "default hotkey clears the warning");
            Panel.ApplyHotkey(2, "ctrl+alt+t"); Check(State.HideHotkey == Hotkeys.DefaultHide && Panel.Notice.Text == L.Get("hotkey_taken"), "a key already used by another function is rejected");
            Panel.ApplyHotkey(2, "ctrl+alt+h"); Panel.ApplyHotkey(3, "f8"); Check(State.HideHotkey == "ctrl+alt+h" && State.BubbleHotkey == "f8", "hide and bubble hotkeys are rebindable");
            Panel.ApplyHotkey(2, Hotkeys.DefaultHide); Panel.ApplyHotkey(3, Hotkeys.DefaultBubble);
            SetBubbles(false); pets[0].Say("auto"); Check(pets[0].Visual.Bubble == "" && !State.AutoBubbles, "automatic bubbles stay hidden while muted");
            pets[0].Say("typed", false); Check(pets[0].Visual.Bubble == "typed", "typed messages still show while muted");
            SetBubbles(true); Check(State.AutoBubbles && pets[0].Visual.Bubble == L.Get("bubbles_on"), "bubbles toggle back on");
            var sync = new PetWindow(this, "동기", "cat", "", 300, false, true) { RemoteId = "sync-1" }; sync.Show();
            double span = Screen.Work.Right - Screen.Work.Left - sync.Width * sync.Dpi;
            sync.Apply(new PetEvent { Kind = "state", UserId = "sync-1", Name = "동기", Species = "cat", X = .9, Lift = .5, Walking = true }); double x0 = sync.CenterX; for (int i = 0; i < 60; i++) sync.Step(.033);
            Check(Math.Abs(sync.FeetY - GroundY) < 1 && Math.Abs(sync.CenterX - x0) > 5 && Math.Abs(sync.CenterX - x0) < 200, "friend copy walks on its own and ignores position fields");
            sync.Apply(new PetEvent { Kind = "parachute", UserId = "sync-1", Name = "동기", Species = "cat", X = .5, Lift = .4 }); for (int i = 0; i < 6; i++) sync.Step(.033);
            Check(sync.Visual.Parachute && sync.IsFalling && Math.Abs(sync.CenterX - (Screen.Work.Left + .5 * span + sync.Width * sync.Dpi / 2)) < 2, "friend parachute starts where the friend dropped");
            for (int i = 0; i < 900 && sync.IsFalling; i++) sync.Step(.033); Check(!sync.IsFalling && Math.Abs(sync.FeetY - GroundY) < 1, "friend copy lands on its own");
            sync.Apply(new PetEvent { Kind = "balloon", UserId = "sync-1", Name = "동기", Species = "cat", X = .3, Lift = .3 }); for (int i = 0; i < 900 && !sync.IsPerched; i++) sync.Step(.033);
            Check(sync.IsPerched && Math.Abs(sync.FeetY - (GroundY - .3 * (GroundY - Screen.Work.Top))) < 1, "friend balloon ride ends on the reported ledge");
            sync.Apply(new PetEvent { Kind = "land", UserId = "sync-1", Name = "동기", Species = "cat", X = .3, Lift = 0 }); sync.Step(.033); Check(!sync.IsPerched && Math.Abs(sync.FeetY - GroundY) < 1, "friend ledge clears on landing");
            sync.Close();
            State.Sleeping = true; snapshotDue = false; pets[0].Step(.033); Check(snapshotDue, "sleep change is sent to friends"); State.Sleeping = false; pets[0].Step(.033); snapshotDue = false;
            State.IdleMinutes = 10; State.NightSleep = false; StateStore.Save(State); var reloaded = StateStore.Load(); Check(reloaded.IdleMinutes == 10 && !reloaded.NightSleep, "settings persist"); State.IdleMinutes = 5; State.NightSleep = true;
            State.Species = "cat"; State.Name = "모찌"; State.Fullness = 86; State.Happiness = 94;
            StateStore.Save(State); var loaded = StateStore.Load(); Check(loaded.Name == State.Name && loaded.Experience == State.Experience, "state persistence round-trip");
            Panel.FriendsCheck.IsChecked = true; Panel.Refresh(); Panel.UpdateLayout();
            Export(Panel, Path.Combine(smokePath!, "preview.png"));
            State.NameStyle = 2; foreach (var p in pets) { p.Step(.033); p.Visual.Bubble = ""; p.Visual.InvalidateVisual(); p.UpdateLayout(); }
            Export(pets[0], Path.Combine(smokePath!, "pet.png")); State.NameStyle = 1;
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
