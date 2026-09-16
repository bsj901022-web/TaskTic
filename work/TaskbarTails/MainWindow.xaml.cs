using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;

namespace TaskbarTails;

public partial class MainWindow : Window
{
    readonly App app;
    bool ready;
    int shownLevel = -1;
    public bool AllowClose;
    static readonly int[] IdleOptions = { 0, 3, 5, 10, 15 };
    public MainWindow(App owner)
    {
        app = owner; InitializeComponent();
        var s = app.State;
        NameBox.Text = s.Name;
        var kinds = PetCatalog.Selectable;
        SpeciesBox.Items.Clear(); foreach (var k in kinds) SpeciesBox.Items.Add(L.Group(k.Group) + " · " + PetCatalog.Label(k));
        SpeciesBox.SelectedIndex = Math.Max(0, Array.FindIndex(kinds, k => k.Id == s.Species));
        VersionBadge.Text = "●  PIXEL PETS · " + app.VersionLabel + " · " + L.F("kinds", kinds.Length) + (StateStore.IsInstalled ? "" : L.Get("badge_portable"));
        FriendsCheck.IsChecked = s.DemoFriends;
        ScaleBox.SelectedIndex = s.Scale == 150 ? 1 : s.Scale == 200 ? 2 : 0;
        LanguageBox.SelectedIndex = s.Language == "ko" ? 1 : s.Language == "en" ? 2 : 0;
        var monitors = Native.Monitors(); MonitorBox.Items.Clear();
        for (int i = 0; i < monitors.Count; i++) { var m = monitors[i]; MonitorBox.Items.Add(L.F("monitor_item", i + 1, m.Monitor.Right - m.Monitor.Left, m.Monitor.Bottom - m.Monitor.Top, m.IsPrimary ? L.Get("monitor_primary") : "")); }
        MonitorBox.SelectedIndex = Math.Clamp(s.MonitorIndex, 0, monitors.Count - 1);
        IdleBox.Items.Clear(); foreach (var m in IdleOptions) IdleBox.Items.Add(m == 0 ? L.Get("idle_off") : L.F("idle_min", m));
        IdleBox.SelectedIndex = Math.Max(0, Array.IndexOf(IdleOptions, s.IdleMinutes));
        StartupCheck.IsChecked = s.StartWithWindows; HotkeyCheck.IsChecked = s.HotkeysEnabled; NightCheck.IsChecked = s.NightSleep;
        StretchCheck.IsChecked = s.StretchReminder; SoundCheck.IsChecked = s.ClickSound; GreetCheck.IsChecked = s.GreetFriends; RejoinCheck.IsChecked = s.AutoRejoin;
        FillBubbleStyles();
        UpdateHotkeyTexts(); PreviewKeyDown += Window_PreviewKeyDown;
        foreach (var r in QuickChatWindow.Reactions)
        {
            var b = new Button { Content = r, FontSize = 15, Padding = new Thickness(9, 4, 9, 4), Margin = new Thickness(0, 0, 6, 4) };
            b.Click += (_, _) => { app.SendBubble(r); Notice.Text = app.Room?.Connected == true ? L.Get("bubble_sent_room") : L.Get("bubble_sent_local"); };
            ReactionPanel.Children.Add(b);
        }
        BuildContacts();
        ready = true;
        Refresh();
        Closing += OnClosing;
    }
    // Contact and support links. Everything opens through the shell (mail app, browser, Explorer).
    const string ContactEmail = "bsj_2200@naver.com", Instagram = "Rinsomnia__", Repo = "https://github.com/bsj901022-web/TaskTic";
    void BuildContacts()
    {
        ContactPanel.Children.Clear();
        AddContact(L.F("contact_email", ContactEmail), () => OpenExternal("mailto:" + ContactEmail + "?subject=" + Uri.EscapeDataString("Taskbar Tails " + app.VersionLabel)), true);
        AddContact(L.Get("contact_copy_email"), () => { Clipboard.SetText(ContactEmail); Notice.Text = L.F("copied", ContactEmail); });
        AddContact(L.F("contact_instagram", Instagram), () => OpenExternal("https://www.instagram.com/" + Instagram + "/"));
        AddContact(L.Get("contact_github"), () => OpenExternal(Repo));
        AddContact(L.Get("contact_issues"), () => OpenExternal(Repo + "/issues"));
        AddContact(L.Get("contact_releases"), () => OpenExternal(Repo + "/releases"));
        AddContact(L.Get("open_data"), () => OpenFolder(Path.GetDirectoryName(StateStore.PathName)!));
        AddContact(L.Get("open_logs"), () => OpenFolder(AppContext.BaseDirectory));
        AboutMade.Text = L.F("about_made", app.VersionLabel);
    }
    void AddContact(string label, Action action, bool primary = false)
    {
        var b = new Button { Content = label, Padding = new Thickness(11, 7, 11, 7), Margin = new Thickness(0, 0, 6, 6), FontSize = 12 };
        if (primary) { b.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(54, 118, 95)); b.Foreground = System.Windows.Media.Brushes.White; }
        b.Click += (_, _) => { try { action(); } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { Notice.Text = L.F("open_failed", e.Message); } };
        ContactPanel.Children.Add(b);
    }
    void OpenExternal(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    void OpenFolder(string path) { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); }
    void OnClosing(object? sender, CancelEventArgs e) { if (!app.Exiting && !AllowClose) { e.Cancel = true; Hide(); } }
    void FillBubbleStyles()
    {
        ready = false;
        BubbleStyleBox.Items.Clear();
        for (int i = 0; i < 4; i++) { int lv = PetState.UnlockLevel(i); string name = L.Get("style_" + i); BubbleStyleBox.Items.Add(app.State.Level >= lv ? name : L.F("style_locked", name, lv)); }
        BubbleStyleBox.SelectedIndex = Math.Clamp(app.State.BubbleStyle, 0, 3);
        shownLevel = app.State.Level; ready = true;
    }
    public void Refresh()
    {
        var s = app.State;
        PetTitle.Text = s.Name; LevelLabel.Text = L.F("level", s.Level);
        FoodLabel.Text = $"{s.Fullness:0} / 100"; FoodBar.Value = s.Fullness;
        HappyLabel.Text = $"{s.Happiness:0} / 100"; HappyBar.Value = s.Happiness;
        XpLabel.Text = $"{s.Experience % 100} / 100"; XpBar.Value = s.Experience % 100;
        MoodLabel.Text = s.Sleeping ? L.Get("mood_sleep") : s.Fullness < 25 ? L.Get("mood_hungry") : L.Get("mood_ok");
        SleepButton.Content = s.Sleeping ? L.Get("wake") : L.Get("sleep");
        var kind = PetCatalog.Get(s.Species); ActionOne.Content = PetCatalog.FirstLabel(kind); ActionTwo.Content = PetCatalog.SecondLabel(kind);
        Preview.Species = s.Species; Preview.Sleeping = s.Sleeping; Preview.ShowName = false; Preview.FrontView = true;
        Preview.InvalidateVisual();
        VisibilityButton.Content = app.PetsVisible ? L.Get("hide_pets") : L.Get("show_pets");
        RoomInfo.Text = app.RoomSummary; RoomInfo.Visibility = RoomInfo.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (shownLevel != s.Level) FillBubbleStyles();
        if (StateStore.LastError != null) Notice.Text = StateStore.LastError;
    }
    public void Animate(double time) { if (!IsVisible) return; Preview.Phase = time; Preview.InvalidateVisual(); }
    void Save() => StateStore.Save(app.State);
    void Feed_Click(object sender, RoutedEventArgs e) => app.Feed();
    void Play_Click(object sender, RoutedEventArgs e) => app.Play();
    void Sleep_Click(object sender, RoutedEventArgs e) => app.ToggleSleep();
    void Apply_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { Notice.Text = L.Get("name_required"); return; }
        var kinds = PetCatalog.Selectable; app.State.Name = name; app.State.Species = kinds[Math.Clamp(SpeciesBox.SelectedIndex, 0, kinds.Length - 1)].Id;
        app.Changed(L.Get("new_look")); Notice.Text = L.Get("saved_look");
    }
    void Scale_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        app.State.Scale = ScaleBox.SelectedIndex switch { 1 => 150, 2 => 200, _ => 100 };
        Save(); Notice.Text = L.F("size_changed", app.State.Scale);
    }
    void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        string lang = LanguageBox.SelectedIndex switch { 1 => "ko", 2 => "en", _ => "auto" };
        if (lang != app.State.Language) app.SetLanguage(lang);
    }
    void Monitor_Changed(object sender, SelectionChangedEventArgs e) { if (!ready) return; app.State.MonitorIndex = Math.Max(0, MonitorBox.SelectedIndex); Save(); app.RefreshScreen(); }
    void Idle_Changed(object sender, SelectionChangedEventArgs e) { if (!ready) return; app.State.IdleMinutes = IdleOptions[Math.Clamp(IdleBox.SelectedIndex, 0, IdleOptions.Length - 1)]; Save(); }
    void BubbleStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        int style = Math.Clamp(BubbleStyleBox.SelectedIndex, 0, 3);
        if (app.State.Level < PetState.UnlockLevel(style)) { Notice.Text = L.F("style_locked", L.Get("style_" + style), PetState.UnlockLevel(style)); ready = false; BubbleStyleBox.SelectedIndex = app.State.EffectiveBubbleStyle; ready = true; return; }
        app.State.BubbleStyle = style; Save();
    }
    bool capturing;
    void UpdateHotkeyTexts()
    {
        string label = Hotkeys.Label(app.State.ChatHotkey);
        capturing = false;
        HotkeyCapture.Content = app.State.ChatHotkey == Hotkeys.Default ? L.F("hotkey_default", label) : label;
        QuickTip.Text = L.F("quick_tip", label);
        bool conflict = Hotkeys.Conflicts(app.State.ChatHotkey);
        HotkeyNote.Text = conflict ? L.Get("hotkey_conflict") : ""; HotkeyNote.Visibility = conflict ? Visibility.Visible : Visibility.Collapsed;
    }
    // Click the hotkey box, then press the combination you want. Esc cancels; modifiers alone are ignored.
    void HotkeyCapture_Click(object sender, RoutedEventArgs e) { capturing = true; HotkeyCapture.Content = L.Get("hotkey_press"); HotkeyCapture.Focus(); }
    void HotkeyReset_Click(object sender, RoutedEventArgs e) => ApplyHotkey(Hotkeys.Default);
    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!capturing) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { UpdateHotkeyTexts(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        string combo = Hotkeys.Compose(Keyboard.Modifiers, key);
        if (!Hotkeys.IsValid(combo)) { Notice.Text = L.Get("hotkey_invalid"); return; }
        ApplyHotkey(combo);
    }
    public void ApplyHotkey(string combo)
    {
        app.State.ChatHotkey = combo; Save(); UpdateHotkeyTexts(); app.RegisterHotkeys(); app.RefreshTrayMenu();
        Notice.Text = L.F("hotkey_saved", Hotkeys.Label(combo));
    }
    void Startup_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.StartWithWindows = StartupCheck.IsChecked == true; Save(); app.ApplyStartup(app.State.StartWithWindows); }
    void Hotkey_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.HotkeysEnabled = HotkeyCheck.IsChecked == true; Save(); app.RegisterHotkeys(); }
    void Night_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.NightSleep = NightCheck.IsChecked == true; Save(); }
    void Stretch_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.StretchReminder = StretchCheck.IsChecked == true; Save(); }
    void Sound_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.ClickSound = SoundCheck.IsChecked == true; Save(); Sounds.Pop(app.State.ClickSound); }
    void Greet_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.GreetFriends = GreetCheck.IsChecked == true; Save(); }
    void Rejoin_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.AutoRejoin = RejoinCheck.IsChecked == true; Save(); }
    void Friends_Changed(object sender, RoutedEventArgs e) { if (ready) app.SetFriends(FriendsCheck.IsChecked == true); }
    void Visibility_Click(object sender, RoutedEventArgs e) => app.ToggleVisible();
    void ActionOne_Click(object sender, RoutedEventArgs e) => app.Specialty(0);
    void ActionTwo_Click(object sender, RoutedEventArgs e) => app.Specialty(1);
    void Room_Click(object sender, RoutedEventArgs e) => app.ShowRoom();
    void Bubble_Click(object sender, RoutedEventArgs e) => SendBubble();
    void Bubble_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) { SendBubble(); e.Handled = true; } }
    void SendBubble()
    {
        var text = BubbleBox.Text.Trim(); if (text.Length == 0) { Notice.Text = L.Get("bubble_empty"); return; }
        app.SendBubble(text); BubbleBox.Clear();
        Notice.Text = app.Room?.Connected == true ? L.Get("bubble_sent_room") : L.Get("bubble_sent_local");
    }
    void Quit_Click(object sender, RoutedEventArgs e) => app.Quit();
    void CheckUpdate_Click(object sender, RoutedEventArgs e) { Notice.Text = L.Get("checking_update"); _ = app.CheckForUpdates(true); }
    void Update_Click(object sender, RoutedEventArgs e) => app.ApplyUpdate();
    public void ShowUpdateReady(string version) { UpdateButton.Content = L.F("update_ready_btn", version); UpdateButton.Visibility = Visibility.Visible; Notice.Text = L.F("update_ready", version); }
}
