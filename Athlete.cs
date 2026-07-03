namespace TrackAndField;

/// <summary>
/// Pluggable athlete renderer. Events draw through the static <see cref="Athlete"/> facade;
/// assign <c>Athlete.Renderer = new SpriteAthlete { ... }</c> to swap the vector skeleton
/// for sprite-sheet frames (see README).
/// </summary>
public interface IAthleteRenderer
{
    void Run(int x, int y, double phase, double speed01, uint jersey, bool flip);
    void Crouch(int x, int y, uint jersey, bool flip);
    void Fly(int x, int y, uint jersey, double legDeg, double armDeg, bool flip);
    void Throw(int x, int y, double t, uint jersey, bool flip, Action<int, int>? handAt);
    void Spin(int x, int y, double phase, uint jersey, out int hx, out int hy);
    void Celebrate(int x, int y, double t, uint jersey);
    void Fallen(int x, int y, uint jersey);
}

/// <summary>Static facade kept for call-site stability; delegates to the active renderer.</summary>
public static class Athlete
{
    public static IAthleteRenderer Renderer = new VectorAthlete();

    public static void Run(int x, int y, double phase, double speed01, uint jersey, bool flip = false)
        => Renderer.Run(x, y, phase, speed01, jersey, flip);
    public static void Crouch(int x, int y, uint jersey, bool flip = false)
        => Renderer.Crouch(x, y, jersey, flip);
    public static void Fly(int x, int y, uint jersey, double legDeg = 45, double armDeg = -140, bool flip = false)
        => Renderer.Fly(x, y, jersey, legDeg, armDeg, flip);
    public static void Throw(int x, int y, double t, uint jersey, bool flip = false, Action<int, int>? handAt = null)
        => Renderer.Throw(x, y, t, jersey, flip, handAt);
    public static void Spin(int x, int y, double phase, uint jersey, out int hx, out int hy)
        => Renderer.Spin(x, y, phase, jersey, out hx, out hy);
    public static void Celebrate(int x, int y, double t, uint jersey)
        => Renderer.Celebrate(x, y, t, jersey);
    public static void Fallen(int x, int y, uint jersey)
        => Renderer.Fallen(x, y, jersey);
}

/// <summary>
/// Procedural pixel athlete: a small articulated skeleton rendered with thick lines.
/// (x, y) = ground point under the hips. Faces +X unless flipped. Sports a moustache.
/// </summary>
public class VectorAthlete : IAthleteRenderer
{
    private const double D2R = Math.PI / 180.0;
    private static readonly uint Moustache = Gfx.Rgb(70, 45, 15);

    private static void Limb(int x0, int y0, double angDeg, double len1, double kneeDeg, double len2,
                             uint c, int m, out int fx, out int fy)
    {
        // ang measured from straight-down, positive = forward (+X). m = mirror (-1 when flipped).
        double a1 = angDeg * D2R;
        int kx = x0 + (int)Math.Round(Math.Sin(a1) * len1) * m;
        int ky = y0 + (int)Math.Round(Math.Cos(a1) * len1);
        double a2 = (angDeg + kneeDeg) * D2R;
        fx = kx + (int)Math.Round(Math.Sin(a2) * len2) * m;
        fy = ky + (int)Math.Round(Math.Cos(a2) * len2);
        Gfx.Line(x0, y0, kx, ky, c, 2);
        Gfx.Line(kx, ky, fx, fy, c, 2);
    }

    private static void Head(int cx, int cy, int m)
    {
        Gfx.FillRect(cx - 2, cy - 2, 4, 4, Gfx.Skin);
        Gfx.HLine(cx - 2, cy - 3, 4, Gfx.Black);          // hair
        Gfx.HLine(cx - 1 + m, cy + 1, 2, Moustache);      // moustache on the facing side
    }

    public void Run(int x, int y, double phase, double speed01, uint jersey, bool flip)
    {
        int m = flip ? -1 : 1;
        double s = 20 + 35 * Math.Clamp(speed01, 0, 1); // stride amplitude in degrees
        int hipY = y - 12, shX = x + 2 * m, shY = hipY - 8;

        // back arm first (drawn under body); elbows pump bent FORWARD
        Limb(shX, shY, Math.Sin(phase + Math.PI) * s * 0.8, 4, 65, 4, Gfx.Rgb(200, 150, 100), m, out _, out _);
        // legs: knee flexes BACKWARD (heel kicks up behind) while the leg swings through
        Limb(x, hipY, Math.Sin(phase) * s, 6, -Math.Max(0, Math.Cos(phase)) * 80, 6, Gfx.Skin, m, out _, out _);
        Limb(x, hipY, Math.Sin(phase + Math.PI) * s, 6, -Math.Max(0, -Math.Cos(phase)) * 80, 6, Gfx.Skin, m, out _, out _);
        // torso leaning forward
        Gfx.Line(x, hipY, shX, shY, jersey, 3);
        Gfx.FillRect(x - 2, hipY - 1, 5, 3, Gfx.White); // shorts
        // front arm
        Limb(shX, shY, Math.Sin(phase) * s * 0.8, 4, 65, 4, Gfx.Skin, m, out _, out _);
        Head(shX + 1 * m, shY - 4, m);
    }

    public void Crouch(int x, int y, uint jersey, bool flip)
    {
        int m = flip ? -1 : 1;
        int hipY = y - 6;
        Limb(x, hipY, 30, 4, 90, 5, Gfx.Skin, m, out _, out _);
        Limb(x, hipY, -40, 4, 110, 5, Gfx.Skin, m, out _, out _);
        int shX = x + 5 * m, shY = hipY - 4;
        Gfx.Line(x, hipY, shX, shY, jersey, 3);
        Gfx.Line(shX, shY, shX + 1 * m, y, Gfx.Skin, 1); // arm to ground
        Head(shX + 2 * m, shY - 4, m);
    }

    public void Fly(int x, int y, uint jersey, double legDeg, double armDeg, bool flip)
    {
        int m = flip ? -1 : 1;
        int hipY = y - 12, shX = x + 1 * m, shY = hipY - 8;
        Limb(x, hipY, legDeg, 6, 20, 6, Gfx.Skin, m, out _, out _);
        Limb(x, hipY, legDeg - 15, 6, 20, 6, Gfx.Skin, m, out _, out _);
        Gfx.Line(x, hipY, shX, shY, jersey, 3);
        Gfx.FillRect(x - 2, hipY - 1, 5, 3, Gfx.White);
        Limb(shX, shY, armDeg, 4, 20, 4, Gfx.Skin, m, out _, out _);
        Limb(shX, shY, armDeg + 30, 4, 20, 4, Gfx.Skin, m, out _, out _);
        Head(shX + 1 * m, shY - 4, m);
    }

    public void Throw(int x, int y, double t, uint jersey, bool flip, Action<int, int>? handAt)
    {
        int m = flip ? -1 : 1;
        int hipY = y - 12, shX = x, shY = hipY - 8;
        Limb(x, hipY, 25, 6, 20, 6, Gfx.Skin, m, out _, out _);
        Limb(x, hipY, -30, 6, 40, 6, Gfx.Skin, m, out _, out _);
        Gfx.Line(x, hipY, shX, shY, jersey, 3);
        Gfx.FillRect(x - 2, hipY - 1, 5, 3, Gfx.White);
        // throwing arm sweeps from behind (-160) over the top to forward (-20)
        double armDeg = -160 + 140 * Math.Clamp(t, 0, 1);
        double a = armDeg * D2R;
        int hx = shX + (int)Math.Round(Math.Sin(a) * 8) * m;
        int hy = shY + (int)Math.Round(Math.Cos(a) * 8);
        Gfx.Line(shX, shY, hx, hy, Gfx.Skin, 2);
        Head(shX + 1 * m, shY - 4, m);
        handAt?.Invoke(hx, hy);
    }

    public void Spin(int x, int y, double phase, uint jersey, out int hx, out int hy)
    {
        int hipY = y - 12, shY = hipY - 8;
        int bx = x + (int)Math.Round(Math.Cos(phase) * 2);
        Gfx.Line(x - 2, y, x, hipY, Gfx.Skin, 2);
        Gfx.Line(x + 2, y, x, hipY, Gfx.Skin, 2);
        Gfx.Line(x, hipY, bx, shY, jersey, 3);
        Gfx.FillRect(x - 2, hipY - 1, 5, 3, Gfx.White);
        Head(bx, shY - 4, 1);
        // arms + wire toward the hammer ball; ellipse squashes Y to fake perspective
        hx = bx + (int)Math.Round(Math.Cos(phase) * 16);
        hy = shY + (int)Math.Round(Math.Sin(phase) * 6);
        Gfx.Line(bx, shY, hx, hy, Gfx.Skin, 1);
        Gfx.Circle(hx, hy, 2, Gfx.DarkGray);
    }

    public void Celebrate(int x, int y, double t, uint jersey)
    {
        int hop = (int)(Math.Abs(Math.Sin(t * 6)) * 3);
        int gy = y - hop;
        int hipY = gy - 12, shY = hipY - 8;
        Gfx.Line(x - 2, gy, x, hipY, Gfx.Skin, 2);
        Gfx.Line(x + 2, gy, x, hipY, Gfx.Skin, 2);
        Gfx.Line(x, hipY, x, shY, jersey, 3);
        Gfx.FillRect(x - 2, hipY - 1, 5, 3, Gfx.White);
        Gfx.Line(x, shY, x - 5, shY - 6, Gfx.Skin, 2);
        Gfx.Line(x, shY, x + 5, shY - 6, Gfx.Skin, 2);
        Head(x, shY - 4, 1);
    }

    public void Fallen(int x, int y, uint jersey)
    {
        Gfx.Line(x - 8, y - 2, x + 4, y - 2, jersey, 3);
        Gfx.Line(x + 4, y - 2, x + 9, y - 3, Gfx.Skin, 2);
        Gfx.FillRect(x + 9, y - 5, 4, 4, Gfx.Skin);
        Gfx.HLine(x + 10, y - 2, 2, Moustache);
        Gfx.Line(x - 8, y - 2, x - 12, y - 1, Gfx.Skin, 2);
    }
}

/// <summary>
/// Sprite-sheet athlete: replaces poses with string-art frames anchored at bottom-center.
/// Any pose left null falls back to the vector renderer, so partial sheets work.
/// Frame chars: 'J' = jersey color, 's' = skin, 'h' = hair/dark, 'w' = white, '.' = transparent.
/// </summary>
public class SpriteAthlete : IAthleteRenderer
{
    private readonly IAthleteRenderer _fallback = new VectorAthlete();

    public string[][]? RunFrames;     // cycled by run phase
    public string[]? CrouchFrame;
    public string[]? FlyFrame;
    public string[]? ThrowFrame;
    public string[]? FallenFrame;

    /// <summary>Optional palette override: (char, jersey) -> color.</summary>
    public Func<char, uint, uint>? Palette;

    private uint Pal(char c, uint jersey) => Palette?.Invoke(c, jersey) ?? c switch
    {
        'J' => jersey,
        's' => Gfx.Skin,
        'h' => Gfx.Black,
        'w' => Gfx.White,
        _ => Gfx.White,
    };

    private void Blit(string[] frame, int x, int y, uint jersey, bool flip)
    {
        int w = frame[0].Length, h = frame.Length;
        Gfx.Sprite(x - w / 2, y - h, frame, c => Pal(c, jersey), flip);
    }

    public void Run(int x, int y, double phase, double speed01, uint jersey, bool flip)
    {
        if (RunFrames is { Length: > 0 })
            Blit(RunFrames[(int)(phase / Math.PI) % RunFrames.Length], x, y, jersey, flip);
        else _fallback.Run(x, y, phase, speed01, jersey, flip);
    }

    public void Crouch(int x, int y, uint jersey, bool flip)
    {
        if (CrouchFrame != null) Blit(CrouchFrame, x, y, jersey, flip);
        else _fallback.Crouch(x, y, jersey, flip);
    }

    public void Fly(int x, int y, uint jersey, double legDeg, double armDeg, bool flip)
    {
        if (FlyFrame != null) Blit(FlyFrame, x, y, jersey, flip);
        else _fallback.Fly(x, y, jersey, legDeg, armDeg, flip);
    }

    public void Throw(int x, int y, double t, uint jersey, bool flip, Action<int, int>? handAt)
    {
        if (ThrowFrame != null) { Blit(ThrowFrame, x, y, jersey, flip); handAt?.Invoke(x + 6, y - 20); }
        else _fallback.Throw(x, y, t, jersey, flip, handAt);
    }

    public void Spin(int x, int y, double phase, uint jersey, out int hx, out int hy)
        => _fallback.Spin(x, y, phase, jersey, out hx, out hy);

    public void Celebrate(int x, int y, double t, uint jersey)
        => _fallback.Celebrate(x, y, t, jersey);

    public void Fallen(int x, int y, uint jersey)
    {
        if (FallenFrame != null) Blit(FallenFrame, x, y, jersey, false);
        else _fallback.Fallen(x, y, jersey);
    }
}

/// <summary>Tiny prop / easter-egg sprites, authored as char rows.</summary>
public static class Props
{
    public static readonly string[] Mole =
    {
        "..bbbb..",
        ".bbbbbb.",
        "bwb..bwb",
        "bbbppbbb",
        ".bbbbbb.",
        ".b.bb.b.",
    };

    public static readonly string[] Bird =
    {
        "w......w",
        ".w....w.",
        "..wwww..",
        ".wwwwwo.",
        "..wwww..",
        "...ll...",
    };

    // Tutankham explorer carrying a key
    public static readonly string[] Tut =
    {
        "..ggg...",
        "..gsg...",
        "..sss.k.",
        ".gggg.k.",
        "g.gg.kk.",
        "..gg....",
        ".g..g...",
        ".g...g..",
    };

    public static uint Pal(char c) => c switch
    {
        'b' => Gfx.Brown,
        'w' => Gfx.White,
        'o' => Gfx.Yellow,
        'l' => Gfx.Rgb(255, 160, 0),
        'g' => Gfx.Rgb(0, 200, 100),
        's' => Gfx.Skin,
        'k' => Gfx.Yellow,
        'p' => Gfx.Rgb(255, 120, 160),
        _ => Gfx.White,
    };

    public static void DrawHurdle(int x, int y, bool knocked)
    {
        if (!knocked)
        {
            Gfx.VLine(x, y - 14, 14, Gfx.White);
            Gfx.VLine(x + 5, y - 14, 14, Gfx.White);
            Gfx.FillRect(x - 1, y - 15, 8, 3, Gfx.Red);
        }
        else
        {
            Gfx.Line(x, y, x + 10, y - 4, Gfx.White);
            Gfx.FillRect(x + 9, y - 6, 3, 3, Gfx.Red);
        }
    }
}
