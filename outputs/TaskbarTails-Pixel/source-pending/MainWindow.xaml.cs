using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;

namespace TaskbarTails;

public partial class MainWindow : Window
{
    readonly App app;
    bool ready;
    int shownLevel = -1;
    public bool AllowClose;
    static readonly int[] IdleOptions = { 0, 3, 5, 10, 15 };
    static readonly Brush FoodNormal = new SolidColorBrush(Color.FromRgb(0x78, 0xA5, 0x8B)), FoodFull = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x5A));
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
        StretchCheck.IsChecked = s.StretchReminder; SoundCheck.IsChecked = s.ClickSound; GreetCheck.IsChecked = s.GreetFriends; RejoinCheck.IsChecked = s.AutoRejoin; WindowCheck.IsChecked = s.WindowPlay; BubblesCheck.IsChecked = s.AutoBubbles; EdgeCheck.IsChecked = s.EdgeRoam;
        FillBubbleStyles();
        NameStyleBox.SelectedIndex = Math.Clamp(s.NameStyle, 0, 2);
        UpdateHotkeyTexts(); PreviewKeyDown += Window_PreviewKeyDown;
        foreach (var r in QuickChatWindow.Reactions)
        {
            var b = new Button { Content = r, FontSize = 15, Padding = new Thickness(9, 4, 9, 4), Margin = new Thickness(0, 0, 6, 4) };
            b.Click += (_, _) => { app.SendBubble(r); Notice.Text = app.Room?.Connected == true ? L.Get("bubble_sent_room") : L.Get("bubble_sent_local"); };
            ReactionPanel.Children.Add(b);
        }
        ready = true;
        Refresh();
        Closing += OnClosing;
    }
    void Info_Click(object sender, RoutedEventArgs e) => app.ShowInfo();
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
        FoodLabel.Text = $"{s.Fullness:0} / 100" + (s.IsFull ? "  ·  " + L.Get("full_tag") : ""); FoodBar.Value = s.Fullness; FoodBar.Foreground = s.IsFull ? FoodFull : FoodNormal;
        HappyLabel.Text = $"{s.Happiness:0} / 100"; HappyBar.Value = s.Happiness;
        XpLabel.Text = $"{s.Experience % 100} / 100"; XpBar.Value = s.Experience % 100;
        var next = PetState.NextPerk(s.Level);
        XpNote.Text = L.F("xp_today", s.CareXpToday, PetState.CareCap, s.PassiveXpToday, PetState.PassiveCap, s.SocialXpToday, PetState.SocialCap) + "\n" + (next is { } p ? L.F("next_perk", p.Level, L.Get(p.Key)) : L.Get("perks_done"));
        MoodLabel.Text = s.Sleeping ? L.Get("mood_sleep") : s.Fullness < 25 ? L.Get("mood_hungry") : s.IsFull ? L.Get("mood_full") : L.Get("mood_ok");
        SleepButton.Content = s.Sleeping ? L.Get("wake") : L.Get("sleep");
        var kind = PetCatalog.Get(s.Species); FeedButton.Content = L.Feed(kind.Group); ActionOne.Content = PetCatalog.FirstLabel(kind); ActionTwo.Content = PetCatalog.SecondLabel(kind);
        Preview.Species = s.Species; Preview.Sleeping = s.Sleeping; Preview.ShowName = false; Preview.FrontView = true; Preview.Level = s.Level;
        Preview.InvalidateVisual();
        VisibilityButton.Content = app.PetsVisible ? L.Get("hide_pets") : L.Get("show_pets");
        if (ready && BubblesCheck.IsChecked != s.AutoBubbles) { ready = false; BubblesCheck.IsChecked = s.AutoBubbles; ready = true; }
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
        app.Changed(L.Get("new_look")); app.RefreshMenus(); Notice.Text = L.Get("saved_look");
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
    // Which hotkey box is waiting for a key press: 0 none, 1 quick bubble, 2 hide/show, 3 automatic bubbles.
    int capturing;
    static string HotkeyText(string combo, string fallback) { string label = Hotkeys.Label(combo); return combo == fallback ? L.F("hotkey_default", label) : label; }
    void UpdateHotkeyTexts()
    {
        var s = app.State; capturing = 0;
        HotkeyCapture.Content = HotkeyText(s.ChatHotkey, Hotkeys.Default);
        HideHotkeyCapture.Content = HotkeyText(s.HideHotkey, Hotkeys.DefaultHide);
        BubbleHotkeyCapture.Content = HotkeyText(s.BubbleHotkey, Hotkeys.DefaultBubble);
        QuickTip.Text = L.F("quick_tip", Hotkeys.Label(s.ChatHotkey));
        bool conflict = Hotkeys.Conflicts(s.ChatHotkey) || Hotkeys.Conflicts(s.HideHotkey) || Hotkeys.Conflicts(s.BubbleHotkey);
        HotkeyNote.Text = conflict ? L.Get("hotkey_conflict") : ""; HotkeyNote.Visibility = conflict ? Visibility.Visible : Visibility.Collapsed;
    }
    // Click a hotkey box, then press the combination you want. Esc cancels; modifiers alone are ignored.
    void StartCapture(int which, Button box) { UpdateHotkeyTexts(); capturing = which; box.Content = L.Get("hotkey_press"); box.Focus(); }
    void HotkeyCapture_Click(object sender, RoutedEventArgs e) => StartCapture(1, HotkeyCapture);
    void HideHotkeyCapture_Click(object sender, RoutedEventArgs e) => StartCapture(2, HideHotkeyCapture);
    void BubbleHotkeyCapture_Click(object sender, RoutedEventArgs e) => StartCapture(3, BubbleHotkeyCapture);
    void HotkeyReset_Click(object sender, RoutedEventArgs e) => ApplyHotkey(1, Hotkeys.Default);
    void HideHotkeyReset_Click(object sender, RoutedEventArgs e) => ApplyHotkey(2, Hotkeys.DefaultHide);
    void BubbleHotkeyReset_Click(object sender, RoutedEventArgs e) => ApplyHotkey(3, Hotkeys.DefaultBubble);
    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (capturing == 0) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { UpdateHotkeyTexts(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        string combo = Hotkeys.Compose(Keyboard.Modifiers, key);
        if (!Hotkeys.IsValid(combo)) { Notice.Text = L.Get("hotkey_invalid"); return; }
        ApplyHotkey(capturing, combo);
    }
    public void ApplyHotkey(string combo) => ApplyHotkey(1, combo);
    // which: 1 quick bubble, 2 hide/show, 3 automatic bubbles. The same key cannot serve two functions.
    public void ApplyHotkey(int which, string combo)
    {
        var s = app.State;
        bool taken = which switch { 1 => combo == s.HideHotkey || combo == s.BubbleHotkey, 2 => combo == s.ChatHotkey || combo == s.BubbleHotkey, _ => combo == s.ChatHotkey || combo == s.HideHotkey };
        if (taken) { UpdateHotkeyTexts(); Notice.Text = L.Get("hotkey_taken"); return; }
        if (which == 1) s.ChatHotkey = combo; else if (which == 2) s.HideHotkey = combo; else s.BubbleHotkey = combo;
        Save(); UpdateHotkeyTexts(); app.RegisterHotkeys(); app.RefreshTrayMenu();
        Notice.Text = L.F("hotkey_saved", Hotkeys.Label(combo));
    }
    void NameStyle_Changed(object sender, SelectionChangedEventArgs e) { if (!ready) return; app.State.NameStyle = Math.Clamp(NameStyleBox.SelectedIndex, 0, 2); Save(); }
    void Startup_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.StartWithWindows = StartupCheck.IsChecked == true; Save(); app.ApplyStartup(app.State.StartWithWindows); }
    void Hotkey_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.HotkeysEnabled = HotkeyCheck.IsChecked == true; Save(); app.RegisterHotkeys(); }
    void Night_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.NightSleep = NightCheck.IsChecked == true; Save(); }
    void Stretch_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.StretchReminder = StretchCheck.IsChecked == true; Save(); }
    void Sound_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.ClickSound = SoundCheck.IsChecked == true; Save(); Sounds.Pop(app.State.ClickSound); }
    void Greet_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.GreetFriends = GreetCheck.IsChecked == true; Save(); }
    void Rejoin_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.AutoRejoin = RejoinCheck.IsChecked == true; Save(); }
    void WindowPlay_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.WindowPlay = WindowCheck.IsChecked == true; Save(); }
    void EdgeRoam_Changed(object sender, RoutedEventArgs e) { if (!ready) return; app.State.EdgeRoam = EdgeCheck.IsChecked == true; Save(); }
    void Bubbles_Changed(object sender, RoutedEventArgs e) { if (!ready) return; bool on = BubblesCheck.IsChecked == true; if (on != app.State.AutoBubbles) app.SetBubbles(on); }
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
