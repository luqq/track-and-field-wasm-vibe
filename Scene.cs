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

    /// <summary>Scoreboard band + crowd. camPx scrolls the crowd slightly (parallax).</summary>
    public static void DrawStadium(int camPx)
    {
        Gfx.FillRect(0, 0, Gfx.W, 56, Gfx.Black);              // scoreboard band
        Gfx.FillRect(0, 56, Gfx.W, 32, Gfx.Rgb(24, 24, 64));   // stands base
        int par = camPx / 4;
        for (int y = 58; y < 87; y += 2)
            for (int x = 0; x < Gfx.W; x += 2)
            {
                uint h = Hash(x + par, y);
                if ((h & 7) < 5) // dense 1983 confetti crowd
                {
                    uint c = (h >> 4 & 3) switch
                    {
                        0 => Gfx.White, 1 => Gfx.Yellow, 2 => Gfx.Cyan, _ => Gfx.Rgb(240, 120, 120)
                    };
                    Gfx.Px(x, y, c);
                }
            }
    }

    /// <summary>
    /// Top scoreboard, Hyper Olympic style: SCORE and WORLD RECORD panels,
    /// status/qualify rows and the big boxed timer/mark readout.
    /// </summary>
    public static void DrawScoreboard(EventBase ev)
    {
        Gfx.FillRect(0, 0, Gfx.W, 56, Gfx.Black);

        // SCORE panel
        Gfx.Rect(2, 2, 122, 34, Gfx.Cyan);
        Gfx.Text(8, 5, "SCORE", Gfx.Cyan);
        Gfx.Text(8, 15, $"{Game.HudPlayer} {Game.HudScore:0000000}", Gfx.White);
        for (int i = 0; i < Math.Min(Game.HudLives, 8); i++)
            Gfx.FillRect(8 + i * 6, 28, 4, 4, Gfx.Yellow);
        Gfx.Text(96, 27, $"M{Game.MatchNo}", Gfx.Gray);

        // WORLD RECORD panel
        Gfx.Rect(128, 2, 126, 34, Gfx.Cyan);
        Gfx.Text(155, 5, "WORLD RECORD", Gfx.Cyan);
        string[] pos = { "1ST", "2ND", "3RD" };
        string[] who = { "EEE", "FFF", "GGG" };
        var rec = ev.Records;
        for (int i = 0; i < 3; i++)
            Gfx.Text(136, 13 + i * 8, $"{pos[i]} {rec[i],6:0.00} {who[i]}", Gfx.Yellow);

        // status + second player + qualify rows
        if (ev.HudLeft.Length > 0) Gfx.Text(4, 39, ev.HudLeft, Gfx.White);
        if (Game.TwoPlayersMode)
            Gfx.Text(120, 39, $"{2 - Game.CurrentPlayer}P {Game.OtherScore:0000000}", Gfx.Gray);
        Gfx.Text(4, 48, $"{L.Qualify} {ev.HudQual}", Gfx.Cyan);

        // big boxed readout (running time / current mark)
        Gfx.Rect(188, 37, 66, 19, Gfx.White);
        Gfx.Text(192, 40, ev.HudBox, Gfx.Yellow, 2);
    }

    /// <summary>Light-gray band carrying the event name, flanked by little flags.</summary>
    public static void DrawEventBand(string name)
    {
        Gfx.FillRect(0, 88, Gfx.W, 14, Gfx.LightGray);
        Gfx.HLine(0, 88, Gfx.W, Gfx.White);
        Gfx.HLine(0, 101, Gfx.W, Gfx.Gray);
        Gfx.TextCentered(91, name, Gfx.Black);
        // left: vertical tricolor
        Gfx.FillRect(5, 90, 4, 9, Gfx.Blue);
        Gfx.FillRect(9, 90, 4, 9, Gfx.White);
        Gfx.FillRect(13, 90, 4, 9, Gfx.Red);
        // right: horizontal stripes with canton
        for (int i = 0; i < 9; i++) Gfx.HLine(238, 90 + i, 14, i % 2 == 0 ? Gfx.Red : Gfx.White);
        Gfx.FillRect(238, 90, 6, 5, Gfx.Blue);
    }

    /// <summary>Green infield with two salmon running lanes, 10 m markers and finish tape.</summary>
    public static void DrawTrack(int camPx, int totalCm, string name)
    {
        DrawEventBand(name);
        Gfx.FillRect(0, 102, Gfx.W, Gfx.H - 102, Gfx.Grass);

        // salmon lane bands (rival above, player below)
        Gfx.FillRect(0, RivalY - 26, Gfx.W, 30, Gfx.Salmon);
        Gfx.HLine(0, RivalY - 26, Gfx.W, Gfx.White);
        Gfx.HLine(0, RivalY + 4, Gfx.W, Gfx.White);
        Gfx.FillRect(0, GroundY - 24, Gfx.W, 28, Gfx.Salmon);
        Gfx.HLine(0, GroundY - 24, Gfx.W, Gfx.White);
        Gfx.HLine(0, GroundY + 4, Gfx.W, Gfx.White);

        for (int m = 0; m * 1000 <= totalCm; m++) // every 10 m
        {
            int wx = (int)(m * 1000 * PxPerCm) - camPx;
            if (wx < -20 || wx > Gfx.W + 20) continue;
            Gfx.Text(wx + 2, 108, (m * 10).ToString(), Gfx.White);
            Gfx.VLine(wx, RivalY - 26, 30, Gfx.Rgb(255, 200, 180));
            Gfx.VLine(wx, GroundY - 24, 28, Gfx.Rgb(255, 200, 180));
        }
        // start and finish lines
        int sx = -camPx, fx = (int)(totalCm * PxPerCm) - camPx;
        if (sx > -6 && sx < Gfx.W)
        {
            Gfx.FillRect(sx - 2, RivalY - 26, 3, 30, Gfx.White);
            Gfx.FillRect(sx - 2, GroundY - 24, 3, 28, Gfx.White);
        }
        if (fx > -6 && fx < Gfx.W + 6)
        {
            for (int y = RivalY - 26; y < GroundY + 4; y += 4)
            {
                Gfx.FillRect(fx, y, 2, 2, Gfx.White);
                Gfx.FillRect(fx + 2, y + 2, 2, 2, Gfx.Black);
            }
        }
        Gfx.FillRect(0, 208, Gfx.W, 16, Gfx.Black); // bottom info band
    }

    /// <summary>Numbered chip + name label at the left of a lane (pre-start).</summary>
    public static void LaneTag(int laneY, string num, string name, uint c)
    {
        Gfx.FillRect(2, laneY - 19, 9, 9, Gfx.White);
        Gfx.Text(4, laneY - 18, num, Gfx.Black);
        Gfx.Text(14, laneY - 18, name, c);
    }

    /// <summary>Runway + field for throws/jumps: green field with distance arcs every 10 m past the foul line.</summary>
    public static void DrawField(int camPx, int foulCm, int maxMeters, string name)
    {
        DrawEventBand(name);
        Gfx.FillRect(0, 102, Gfx.W, Gfx.H - 102, Gfx.Grass);
        for (int y = 104; y < 208; y += 8) Gfx.HLine(0, y, Gfx.W, Gfx.GrassDark);
        // runway
        Gfx.FillRect(0, GroundY - 4, (int)(foulCm * PxPerCm) - camPx + 4, 12, Gfx.Salmon);
        Gfx.HLine(0, GroundY - 5, (int)(foulCm * PxPerCm) - camPx + 4, Gfx.White);
        // foul line
        int fl = (int)(foulCm * PxPerCm) - camPx;
        if (fl > -4 && fl < Gfx.W) Gfx.FillRect(fl, GroundY - 6, 3, 14, Gfx.White);
        // arcs
        for (int m = 10; m <= maxMeters; m += 10)
        {
            int wx = (int)((foulCm + m * 100) * PxPerCm) - camPx;
            if (wx < -20 || wx > Gfx.W + 20) continue;
            Gfx.VLine(wx, 110, GroundY - 100, Gfx.White);
            Gfx.Text(wx - 5, GroundY + 8, m.ToString(), Gfx.White);
        }
        Gfx.FillRect(0, 208, Gfx.W, 16, Gfx.Black); // bottom info band
    }

    /// <summary>Bottom readout, original style: SPEED= segmented bar + numeric CM/SEC.</summary>
    public static void SpeedBar(double speedCms)
    {
        Gfx.FillRect(0, 208, Gfx.W, 16, Gfx.Black);
        Gfx.Text(4, 212, $"{L.Speed}=", Gfx.White);
        int seg = (int)(Math.Clamp(speedCms / 1500.0, 0, 1) * 20);
        for (int i = 0; i < 20; i++)
            Gfx.FillRect(46 + i * 5, 211, 4, 9, i < seg ? (i >= 17 ? Gfx.Red : Gfx.Yellow) : Gfx.DarkGray);
        Gfx.Text(160, 212, $"{(int)speedCms:0000}CM/SEC", Gfx.White);
    }

    /// <summary>Boxed launch-angle readout, original style.</summary>
    public static void AngleMeter(double deg)
    {
        Gfx.FillRect(4, 146, 28, 13, Gfx.Black);
        Gfx.Rect(4, 146, 28, 13, Gfx.White);
        Gfx.Text(9, 149, $"{(int)deg,2}~", Gfx.Yellow);
    }
}
