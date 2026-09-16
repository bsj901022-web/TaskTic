using System;
using System.IO;

namespace TaskbarTails;

// One tiny synthesized "pop" (no audio files shipped). Off by default; enabled in settings.
public static class Sounds
{
    static byte[]? pop;
    public static void Pop(bool enabled)
    {
        if (!enabled) return;
        try
        {
            pop ??= Build();
            using var player = new System.Media.SoundPlayer(new MemoryStream(pop));
            player.Load(); player.Play();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException) { }
    }
    static byte[] Build()
    {
        const int rate = 22050; int samples = rate * 90 / 1000;
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + samples * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / rate, envelope = Math.Exp(-t * 38), frequency = 880 - 2600 * t;
            w.Write((short)(Math.Sin(2 * Math.PI * frequency * t) * envelope * 11000));
        }
        w.Flush(); return ms.ToArray();
    }
}
