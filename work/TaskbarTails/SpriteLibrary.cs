using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskbarTails;

public sealed class SpriteSet
{
    public const double WalkFps = 12, ActionFps = 9;
    public readonly Dictionary<string, BitmapSource> Idle = new();
    public readonly Dictionary<string, List<BitmapSource>> Walk = new();
    public readonly Dictionary<string, List<BitmapSource>> Actions = new();
    public Rect Bounds;
    public int FrameCount => Walk.Values.Sum(x => x.Count);
    public bool HasAction(string key) => Actions.TryGetValue(key, out var clip) && clip.Count > 0;
    public int WalkFrames(string direction) => Walk.TryGetValue(direction, out var f) ? f.Count : 0;
    // Returns the frame to draw and whether it must be mirrored horizontally.
    // Special motions exist for east only; a missing west set falls back to the mirrored east set.
    public (BitmapSource Image, bool Flip) Frame(bool left, bool walking, double phase, bool front, string action = "", double actionTime = 0)
    {
        if (action.Length > 0 && Actions.TryGetValue(action, out var clip) && clip.Count > 0)
        {
            int index = Math.Max(0, (int)(actionTime * ActionFps));
            return (clip[PetCatalog.HoldPose.Contains(action) ? Math.Min(clip.Count - 1, index) : index % clip.Count], left && !front);
        }
        string direction = front ? "south" : left ? "west" : "east";
        if (walking && !front)
        {
            if (Walk.TryGetValue(direction, out var frames) && frames.Count > 0) return (frames[(int)(phase * WalkFps) % frames.Count], false);
            string other = left ? "east" : "west";
            if (Walk.TryGetValue(other, out frames) && frames.Count > 0) return (frames[(int)(phase * WalkFps) % frames.Count], true);
        }
        if (Idle.TryGetValue(direction, out var idle)) return (idle, false);
        if (!front && Idle.TryGetValue(left ? "east" : "west", out idle)) return (idle, true);
        return (Idle.Values.First(), false);
    }
}

public static class SpriteLibrary
{
    static readonly Dictionary<string, SpriteSet?> Cache = new();
    public static SpriteSet? Get(string species)
    {
        if (Cache.TryGetValue(species, out var cached)) return cached;
        string root = Path.Combine(AppContext.BaseDirectory, "assets", species);
        var set = new SpriteSet();
        Rect bounds = Rect.Empty;
        try
        {
            if (!Directory.Exists(root)) { Cache[species] = null; return null; }
            BitmapSource Load(string file)
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Path.GetFullPath(file)); bitmap.EndInit(); bitmap.Freeze();
                var rgba = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                int w = rgba.PixelWidth, h = rgba.PixelHeight;
                var bytes = new byte[w * h * 4]; rgba.CopyPixels(bytes, w * 4, 0);
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    if (bytes[(y * w + x) * 4 + 3] > 20) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                if (maxX >= 0) bounds.Union(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
                return bitmap;
            }
            foreach (var direction in new[] { "south", "east", "west" })
            {
                var idle = Path.Combine(root, "idle-" + direction + ".png");
                if (File.Exists(idle)) set.Idle[direction] = Load(idle);
                set.Walk[direction] = Directory.GetFiles(root, "walk-" + direction + "-*.png").OrderBy(x => x, StringComparer.Ordinal).Select(Load).ToList();
            }
            foreach (var group in Directory.GetFiles(root, "action-*-east-*.png").GroupBy(f => Path.GetFileName(f).Split('-')[1]))
                set.Actions[group.Key] = group.OrderBy(x => x, StringComparer.Ordinal).Select(Load).ToList();
            set.Bounds = bounds;
            Cache[species] = set.Idle.Count > 0 && !bounds.IsEmpty ? set : null;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or FileFormatException) { Cache[species] = null; }
        return Cache[species];
    }
}
