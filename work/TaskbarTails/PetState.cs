using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarTails;

public sealed class PetState
{
    public string Name { get; set; } = "모찌";
    public string Species { get; set; } = "cat";
    public double Fullness { get; set; } = 72;
    public double Happiness { get; set; } = 80;
    public int Experience { get; set; }
    public bool Sleeping { get; set; }
    public bool DemoFriends { get; set; }
    public int Scale { get; set; } = 100;            // desktop character size: 100, 150 or 200 percent
    // --- settings (v0.6) ---
    public string Language { get; set; } = "auto";   // auto | ko | en
    public bool StartWithWindows { get; set; }
    public bool HotkeysEnabled { get; set; } = true;  // Ctrl+Alt+P hide/show + the quick-bubble hotkey below
    public string ChatHotkey { get; set; } = "ctrl+alt+t"; // quick bubble
    public string HideHotkey { get; set; } = "ctrl+alt+p"; // hide / show characters
    public string BubbleHotkey { get; set; } = "ctrl+alt+b"; // automatic bubbles on / off
    public bool AutoBubbles { get; set; } = true;          // small talk, reactions, window comments (typed messages always show)
    public bool NightSleep { get; set; } = true;      // 23:00-07:00
    public int IdleMinutes { get; set; } = 5;         // 0 = off
    public bool StretchReminder { get; set; } = true; // every 50 minutes
    public bool ClickSound { get; set; }
    public bool GreetFriends { get; set; } = true;
    public bool AutoRejoin { get; set; } = true;      // rejoin LastRoomCode on start
    public string LastRoomCode { get; set; } = "";
    public int MonitorIndex { get; set; }             // 0 = primary
    public int BubbleStyle { get; set; }              // cosmetic, unlocked by level
    public int NameStyle { get; set; } = 1;           // 0 hidden, 1 small, 2 large label with background
    public bool WindowPlay { get; set; } = true;      // land on / walk along / ride a balloon up to other windows, react to the active window
    public bool EdgeRoam { get; set; } = true;        // climb the left/right screen edges and walk upside down along the top
    public DateTime LastSeenUtc { get; set; }
    [JsonIgnore] public double HoursAway { get; private set; }

    public int Level => 1 + Experience / 100;
    // --- XP economy (v0.6.9): care actions earn XP only within limits, so feed -> play loops cannot level forever.
    // Care (feed when hungry, play every 2 min, a few clicks) up to CareCap a day; time together 1 XP per 10 min up to
    // PassiveCap; pokes and greetings with friends up to SocialCap. About 1.5 levels a day at most.
    public enum XpNote { Ok, NotHungry, PlayCooldown, ClickCap, CareCap, None }
    public const int CareCap = 80, PassiveCap = 48, SocialCap = 20, ClickCap = 12;
    public const double FeedHungerLimit = 70, PlayXpMinutes = 2, PassiveMinutesPerXp = 10;
    public string XpDay { get; set; } = "";
    public int CareXpToday { get; set; }
    public int PassiveXpToday { get; set; }
    public int SocialXpToday { get; set; }
    public int ClickXpToday { get; set; }
    public DateTime LastPlayXpUtc { get; set; }
    public double PassiveSeconds { get; set; }
    [JsonIgnore] public int LastXp { get; private set; }
    [JsonIgnore] public XpNote LastNote { get; private set; } = XpNote.None;
    void RollDay() { string today = DateTime.Now.ToString("yyyy-MM-dd"); if (XpDay != today) { XpDay = today; CareXpToday = 0; PassiveXpToday = 0; SocialXpToday = 0; ClickXpToday = 0; } }
    int GrantCare(int amount) { RollDay(); int gain = Math.Clamp(CareCap - CareXpToday, 0, amount); CareXpToday += gain; Experience += gain; return gain; }
    public int GrantSocial(int amount) { RollDay(); int gain = Math.Clamp(SocialCap - SocialXpToday, 0, amount); SocialXpToday += gain; Experience += gain; return gain; }
    // Time spent together while awake and present: 1 XP per 10 minutes. Returns the XP granted this tick (0 or 1).
    public int TickPresence(double seconds, bool active)
    {
        if (!active || Sleeping) return 0;
        PassiveSeconds += seconds; if (PassiveSeconds < PassiveMinutesPerXp * 60) return 0;
        PassiveSeconds -= PassiveMinutesPerXp * 60; RollDay();
        if (PassiveXpToday >= PassiveCap) return 0; PassiveXpToday++; Experience++; return 1;
    }
    // Rewards by level, shown to friends too: name colour tiers, a level badge, sparkles and a crown.
    public static readonly (int Level, string Key)[] Perks = { (3, "perk_3"), (5, "perk_5"), (8, "perk_8"), (10, "perk_10"), (15, "perk_15"), (20, "perk_20") };
    public static (int Level, string Key)? NextPerk(int level) { foreach (var p in Perks) if (p.Level > level) return p; return null; }
    public static string? PerkAt(int level) { foreach (var p in Perks) if (p.Level == level) return p.Key; return null; }
    public static int UnlockLevel(int style) => style switch { 1 => 3, 2 => 5, 3 => 8, _ => 1 };
    public int EffectiveBubbleStyle => Level >= UnlockLevel(BubbleStyle) ? BubbleStyle : 0;
    public const double FullThreshold = 85;
    public bool IsFull => Fullness >= FullThreshold;
    // False when the character is too full to eat: nothing is eaten and a little XP and happiness are lost instead (never below the current level).
    public bool Feed()
    {
        if (IsFull) { Experience = Math.Max((Level - 1) * 100, Experience - 4); Happiness = Math.Max(0, Happiness - 4); LastXp = -4; LastNote = XpNote.None; return false; }
        bool hungry = Fullness < FeedHungerLimit;
        Fullness = Math.Min(100, Fullness + 18); Happiness = Math.Min(100, Happiness + 3);
        LastXp = hungry ? GrantCare(5) : 0; LastNote = !hungry ? XpNote.NotHungry : LastXp == 0 ? XpNote.CareCap : XpNote.Ok; return true;
    }
    public void Pet()
    {
        Happiness = Math.Min(100, Happiness + 8); RollDay();
        if (ClickXpToday >= ClickCap) { LastXp = 0; LastNote = XpNote.ClickCap; return; }
        LastXp = GrantCare(3); ClickXpToday += LastXp; LastNote = LastXp == 0 ? XpNote.CareCap : XpNote.Ok;
    }
    public void Play()
    {
        Sleeping = false; Happiness = Math.Min(100, Happiness + 12); Fullness = Math.Max(0, Fullness - 4);
        if ((DateTime.UtcNow - LastPlayXpUtc).TotalMinutes < PlayXpMinutes) { LastXp = 0; LastNote = XpNote.PlayCooldown; return; }
        LastXp = GrantCare(8); if (LastXp > 0) LastPlayXpUtc = DateTime.UtcNow; LastNote = LastXp == 0 ? XpNote.CareCap : XpNote.Ok;
    }
    public void Tick(double seconds) { Fullness = Math.Max(0, Fullness - seconds / 90); Happiness = Math.Clamp(Happiness + (Sleeping ? 1 : -1) * seconds / 180, 0, 100); }
    // Time the app was closed counts a little: 2 fullness and 1 happiness per hour, never below a friendly floor.
    public void ApplyOfflineTime(DateTime nowUtc)
    {
        if (LastSeenUtc == default) { HoursAway = 0; return; }
        HoursAway = Math.Clamp((nowUtc - LastSeenUtc).TotalHours, 0, 72);
        Fullness = Math.Max(Math.Min(Fullness, 20), Fullness - HoursAway * 2);
        Happiness = Math.Max(Math.Min(Happiness, 30), Happiness - HoursAway);
    }
}

public static class StateStore
{
    public static string PathName = Path.Combine(DataRoot(), "pet.json");
    // Velopack installs to %LocalAppData%\TaskbarTails\current\ and replaces that folder on every update,
    // so an installed app stores its data in %LocalAppData%\TaskbarTails\data\. A portable copy keeps data beside the exe.
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "Update.exe"));
    static string DataRoot() => IsInstalled ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "data")) : Path.Combine(AppContext.BaseDirectory, "data");
    public static string? LastError { get; private set; }
    public static PetState Load()
    {
        try
        {
            if (!File.Exists(PathName)) return new();
            var state = JsonSerializer.Deserialize<PetState>(File.ReadAllText(PathName)) ?? new();
            state.Name = string.IsNullOrWhiteSpace(state.Name) ? "모찌" : state.Name[..Math.Min(12, state.Name.Length)];
            state.Species = PetCatalog.Valid(state.Species) ? state.Species : "cat";
            state.Fullness = double.IsFinite(state.Fullness) ? Math.Clamp(state.Fullness, 0, 100) : 72;
            state.Happiness = double.IsFinite(state.Happiness) ? Math.Clamp(state.Happiness, 0, 100) : 80;
            state.Experience = Math.Clamp(state.Experience, 0, 1000000);
            state.XpDay ??= ""; state.CareXpToday = Math.Clamp(state.CareXpToday, 0, PetState.CareCap); state.PassiveXpToday = Math.Clamp(state.PassiveXpToday, 0, PetState.PassiveCap);
            state.SocialXpToday = Math.Clamp(state.SocialXpToday, 0, PetState.SocialCap); state.ClickXpToday = Math.Clamp(state.ClickXpToday, 0, PetState.ClickCap);
            state.PassiveSeconds = double.IsFinite(state.PassiveSeconds) ? Math.Clamp(state.PassiveSeconds, 0, PetState.PassiveMinutesPerXp * 60) : 0;
            state.Scale = state.Scale is 150 or 200 ? state.Scale : 100;
            state.Language = state.Language is "ko" or "en" ? state.Language : "auto";
            state.IdleMinutes = state.IdleMinutes is 0 or 3 or 5 or 10 or 15 ? state.IdleMinutes : 5;
            state.MonitorIndex = Math.Clamp(state.MonitorIndex, 0, 8);
            state.BubbleStyle = Math.Clamp(state.BubbleStyle, 0, 3);
            state.NameStyle = Math.Clamp(state.NameStyle, 0, 2);
            state.ChatHotkey = Hotkeys.IsValid(state.ChatHotkey) ? state.ChatHotkey : Hotkeys.Default;
            state.HideHotkey = Hotkeys.IsValid(state.HideHotkey) ? state.HideHotkey : Hotkeys.DefaultHide;
            state.BubbleHotkey = Hotkeys.IsValid(state.BubbleHotkey) ? state.BubbleHotkey : Hotkeys.DefaultBubble;
            if (state.HideHotkey == state.ChatHotkey) state.HideHotkey = Hotkeys.DefaultHide == state.ChatHotkey ? "ctrl+alt+h" : Hotkeys.DefaultHide;
            if (state.BubbleHotkey == state.ChatHotkey || state.BubbleHotkey == state.HideHotkey) state.BubbleHotkey = Hotkeys.DefaultBubble == state.ChatHotkey || Hotkeys.DefaultBubble == state.HideHotkey ? "ctrl+alt+m" : Hotkeys.DefaultBubble;
            state.LastRoomCode = (state.LastRoomCode ?? "").Trim().ToUpperInvariant(); if (state.LastRoomCode.Length > 16) state.LastRoomCode = "";
            state.ApplyOfflineTime(DateTime.UtcNow);
            return state;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { LastError = "저장 파일을 읽지 못해 새 친구로 시작했어요."; return new(); }
    }
    public static void Save(PetState state)
    {
        try
        {
            state.LastSeenUtc = DateTime.UtcNow;
            Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
            File.WriteAllText(PathName + ".tmp", JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(PathName + ".tmp", PathName, true);
            LastError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LastError = "저장 실패 · 쓰기 가능한 폴더에서 실행해 주세요."; }
    }
}
