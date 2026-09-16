using System;
using System.IO;
using System.Text.Json;

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
    public int Scale { get; set; } = 100; // desktop character size: 100, 150 or 200 percent
    public int Level => 1 + Experience / 100;
    public void Feed() { Fullness = Math.Min(100, Fullness + 18); Happiness = Math.Min(100, Happiness + 3); Experience += 5; }
    public void Pet() { Happiness = Math.Min(100, Happiness + 8); Experience += 3; }
    public void Play() { Sleeping = false; Happiness = Math.Min(100, Happiness + 12); Fullness = Math.Max(0, Fullness - 4); Experience += 8; }
    public void Tick(double seconds) { Fullness = Math.Max(0, Fullness - seconds / 90); Happiness = Math.Clamp(Happiness + (Sleeping ? 1 : -1) * seconds / 180, 0, 100); }
}

public static class StateStore
{
    public static string PathName = Path.Combine(AppContext.BaseDirectory, "data", "pet.json");
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
            return state;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { LastError = "저장 파일을 읽지 못해 새 친구로 시작했어요."; return new(); }
    }
    public static void Save(PetState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
            File.WriteAllText(PathName + ".tmp", JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(PathName + ".tmp", PathName, true);
            LastError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LastError = "저장 실패 · 쓰기 가능한 폴더에서 실행해 주세요."; }
    }
}


