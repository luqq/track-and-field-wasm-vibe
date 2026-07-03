using System.Runtime.InteropServices.JavaScript;
using TrackAndField;

Console.WriteLine("TRACK & FIELD (1983) - .NET 10 WASM engine ready");

/// <summary>
/// Interop surface. JS drives the loop via requestAnimationFrame -> Update(ts),
/// and reads the framebuffer directly from linear WASM memory (zero-copy).
/// </summary>
public static partial class Engine
{
    private static double _lastTs = -1;
    private static double _accum;
    private static bool _seeded;
    private const double StepMs = 1000.0 / 60.0;

    /// <summary>Pinned framebuffer address; JS overlays a Uint8ClampedArray on it once.</summary>
    [JSExport]
    public static int GetFrameBufferAddress()
    {
        unsafe
        {
            fixed (uint* p = Gfx.Fb) return (int)p;
        }
    }

    [JSExport]
    public static int GetWidth() => Gfx.W;

    [JSExport]
    public static int GetHeight() => Gfx.H;

    /// <summary>Advance the simulation. Fixed 60 Hz steps, render once per rAF callback.</summary>
    [JSExport]
    public static void Update(double timestamp)
    {
        if (!_seeded) { Game.SeedFrom(timestamp); _seeded = true; }
        if (_lastTs < 0) _lastTs = timestamp;
        _accum += Math.Min(100, timestamp - _lastTs); // clamp tab-switch gaps
        _lastTs = timestamp;

        int steps = 0;
        while (_accum >= StepMs && steps < 4)
        {
            Game.Step();
            _accum -= StepMs;
            steps++;
        }
        if (_accum >= StepMs) _accum = 0; // drop the rest if we fell behind

        Game.Draw();
    }

    /// <summary>0/1 = RUN buttons (not discriminated, faithful to the board), 2 = ACTION, 3 = START.</summary>
    [JSExport]
    public static void OnButton(int button, int isDown) => Input.OnButton(button, isDown != 0);
}
