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
    public string ChatHotkey { get; set; } = "ctrl+alt+t"; // one of Hotkeys.Options
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
    public DateTime LastSeenUtc { get; set; }
    [JsonIgnore] public double HoursAway { get; private set; }

    public int Level => 1 + Experience / 100;
    public static int UnlockLevel(int style) => style switch { 1 => 3, 2 => 5, 3 => 8, _ => 1 };
    public int EffectiveBubbleStyle => Level >= UnlockLevel(BubbleStyle) ? BubbleStyle : 0;
    public void Feed() { Fullness = Math.Min(100, Fullness + 18); Happiness = Math.Min(100, Happiness + 3); Experience += 5; }
    public void Pet() { Happiness = Math.Min(100, Happiness + 8); Experience += 3; }
    public void Play() { Sleeping = false; Happiness = Math.Min(100, Happiness + 12); Fullness = Math.Max(0, Fullness - 4); Experience += 8; }
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
            state.Scale = state.Scale is 150 or 200 ? state.Scale : 100;
            state.Language = state.Language is "ko" or "en" ? state.Language : "auto";
            state.IdleMinutes = state.IdleMinutes is 0 or 3 or 5 or 10 or 15 ? state.IdleMinutes : 5;
            state.MonitorIndex = Math.Clamp(state.MonitorIndex, 0, 8);
            state.BubbleStyle = Math.Clamp(state.BubbleStyle, 0, 3);
            state.NameStyle = Math.Clamp(state.NameStyle, 0, 2);
            state.ChatHotkey = Hotkeys.IsValid(state.ChatHotkey) ? state.ChatHotkey : Hotkeys.Default;
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
