using System.Runtime.InteropServices.JavaScript;

namespace TrackAndField;

/// <summary>Monophonic square-wave audio via Web Audio API (implemented in main.js).</summary>
public static partial class Audio
{
    [JSImport("audio.tone", "main.js")]
    internal static partial void Tone(double freq, double ms, double vol);

    [JSImport("audio.jingle", "main.js")]
    internal static partial void Jingle(int id);

    public const int JingleTitle = 0;
    public const int JingleQualify = 1;
    public const int JingleFail = 2;
    public const int JingleEgg = 3;
    public const int JingleGun = 4;
    public const int JingleRecord = 5;

    public static void Beep() => Tone(880, 50, 0.15);
    public static void LowBeep() => Tone(220, 120, 0.2);
    public static void Tick(double pitch01) => Tone(300 + pitch01 * 700, 30, 0.12);
}
