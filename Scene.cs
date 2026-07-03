namespace TrackAndField;

/// <summary>Shared stadium background rendering. World scale: 1 px = 5 cm (0.02 px/cm... 0.2 px/cm).</summary>
public static class Scene
{
    public const double PxPerCm = 0.2; // 1 px = 5 cm; 100 m = 2000 px of scroll

    public const int GroundY = 190;      // player lane baseline
    public const int RivalY = 158;       // rival lane baseline

    private static uint _crowdSeed = 0x9E3779B9;

    private static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263) ^ _crowdSeed;
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

    /// <summary>Sky + crowd band + wall. camPx scrolls the crowd slightly (parallax).</summary>
    public static void DrawStadium(int camPx)
    {
        Gfx.FillRect(0, 0, Gfx.W, 20, Gfx.Black);              // scoreboard band
        Gfx.FillRect(0, 20, Gfx.W, 44, Gfx.Rgb(24, 24, 64));   // stands base
        int par = camPx / 4;
        for (int y = 24; y < 62; y += 3)
            for (int x = 0; x < Gfx.W; x += 2)
            {
                uint h = Hash(x + par, y);
                if ((h & 7) < 3)
                {
                    uint c = (h >> 4 & 3) switch
                    {
                        0 => Gfx.White, 1 => Gfx.Yellow, 2 => Gfx.Cyan, _ => Gfx.Rgb(240, 120, 120)
                    };
                    Gfx.Px(x, y, c);
                }
            }
        Gfx.FillRect(0, 62, Gfx.W, 6, Gfx.Gray);               // wall
        Gfx.FillRect(0, 68, Gfx.W, 20, Gfx.Grass);             // infield strip
    }

    /// <summary>Two-lane running track with 10 m markers. camPx = camera in world pixels.</summary>
    public static void DrawTrack(int camPx, int totalCm)
    {
        Gfx.FillRect(0, 88, Gfx.W, Gfx.H - 88, Gfx.TrackRed);
        // lane lines
        Gfx.HLine(0, 126, Gfx.W, Gfx.White);
        Gfx.HLine(0, RivalY + 4, Gfx.W, Gfx.White);
        Gfx.HLine(0, GroundY + 4, Gfx.W, Gfx.White);
        Gfx.FillRect(0, GroundY + 8, Gfx.W, Gfx.H - GroundY - 8, Gfx.TrackRedDark);

        for (int m = 0; m * 1000 <= totalCm; m++) // every 10 m
        {
            int wx = (int)(m * 1000 * PxPerCm) - camPx;
            if (wx < -20 || wx > Gfx.W + 20) continue;
            Gfx.VLine(wx, 126, GroundY + 4 - 126, Gfx.Rgb(240, 200, 180));
            Gfx.Text(wx + 2, 130, (m * 10).ToString(), Gfx.White);
        }
        // start and finish lines
        int sx = -camPx, fx = (int)(totalCm * PxPerCm) - camPx;
        if (sx > -6 && sx < Gfx.W) Gfx.FillRect(sx - 2, 126, 3, GroundY + 4 - 126, Gfx.White);
        if (fx > -6 && fx < Gfx.W + 6)
        {
            for (int y = 126; y < GroundY + 4; y += 4)
            {
                Gfx.FillRect(fx, y, 2, 2, Gfx.White);
                Gfx.FillRect(fx + 2, y + 2, 2, 2, Gfx.Black);
            }
        }
    }

    /// <summary>Runway + field for throws/jumps: green field with distance arcs every 10 m past the foul line.</summary>
    public static void DrawField(int camPx, int foulCm, int maxMeters)
    {
        Gfx.FillRect(0, 88, Gfx.W, Gfx.H - 88, Gfx.Grass);
        for (int y = 88; y < Gfx.H; y += 8) Gfx.HLine(0, y, Gfx.W, Gfx.GrassDark);
        // runway
        Gfx.FillRect(0, GroundY - 4, (int)(foulCm * PxPerCm) - camPx + 4, 12, Gfx.TrackRed);
        // foul line
        int fl = (int)(foulCm * PxPerCm) - camPx;
        if (fl > -4 && fl < Gfx.W) Gfx.FillRect(fl, GroundY - 6, 3, 14, Gfx.White);
        // arcs
        for (int m = 10; m <= maxMeters; m += 10)
        {
            int wx = (int)((foulCm + m * 100) * PxPerCm) - camPx;
            if (wx < -20 || wx > Gfx.W + 20) continue;
            Gfx.VLine(wx, 100, GroundY - 90, Gfx.White);
            Gfx.Text(wx - 5, GroundY + 8, m.ToString(), Gfx.White);
        }
    }

    public static void SpeedBar(double speedCms)
    {
        Gfx.Text(8, 200, L.Speed, Gfx.White);
        Gfx.FillRect(48, 200, 154, 7, Gfx.DarkGray);
        int w = (int)(Math.Clamp(speedCms / 1500.0, 0, 1) * 152);
        uint c = speedCms > 1200 ? Gfx.Red : speedCms > 800 ? Gfx.Yellow : Gfx.Cyan;
        Gfx.FillRect(49, 201, w, 5, c);
        // 1300 cm/s javelin cap notch
        Gfx.VLine(49 + (int)(1300 / 1500.0 * 152), 199, 9, Gfx.White);
    }

    public static void AngleMeter(double deg)
    {
        Gfx.Text(206, 200, $"{(int)deg,2}~", Gfx.Yellow);
        int cx = 232, cy = 213;
        double a = deg * Math.PI / 180;
        Gfx.Line(cx, cy, cx + (int)(Math.Cos(a) * 12), cy - (int)(Math.Sin(a) * 12), Gfx.Yellow, 1);
    }
}
