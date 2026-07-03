namespace TrackAndField;

/// <summary>
/// Software renderer over a pinned RGBA framebuffer (256x224, native arcade resolution).
/// JS overlays a Uint8ClampedArray on this memory and blits it with putImageData.
/// </summary>
public static class Gfx
{
    public const int W = 256, H = 224;

    // Pinned so the address handed to JS never moves. Byte order in memory: R,G,B,A.
    public static readonly uint[] Fb = GC.AllocateArray<uint>(W * H, pinned: true);

    public static uint Rgb(int r, int g, int b) => (uint)(r | (g << 8) | (b << 16)) | 0xFF000000u;

    // Konami-ish palette
    public static readonly uint Black = Rgb(0, 0, 0);
    public static readonly uint White = Rgb(248, 248, 248);
    public static readonly uint SkyBlue = Rgb(60, 120, 216);
    public static readonly uint TrackRed = Rgb(200, 76, 12);
    public static readonly uint TrackRedDark = Rgb(168, 56, 8);
    public static readonly uint Grass = Rgb(0, 140, 32);
    public static readonly uint GrassDark = Rgb(0, 112, 24);
    public static readonly uint Sand = Rgb(232, 200, 120);
    public static readonly uint Yellow = Rgb(248, 216, 0);
    public static readonly uint Red = Rgb(216, 40, 24);
    public static readonly uint Blue = Rgb(40, 80, 224);
    public static readonly uint Cyan = Rgb(0, 216, 216);
    public static readonly uint Skin = Rgb(248, 184, 128);
    public static readonly uint Gray = Rgb(140, 140, 140);
    public static readonly uint DarkGray = Rgb(70, 70, 70);
    public static readonly uint Brown = Rgb(140, 90, 40);

    public static void Clear(uint c) => Array.Fill(Fb, c);

    public static void Px(int x, int y, uint c)
    {
        if ((uint)x < W && (uint)y < H) Fb[y * W + x] = c;
    }

    public static void FillRect(int x, int y, int w, int h, uint c)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(W, x + w), y1 = Math.Min(H, y + h);
        for (int yy = y0; yy < y1; yy++)
        {
            int row = yy * W;
            for (int xx = x0; xx < x1; xx++) Fb[row + xx] = c;
        }
    }

    public static void HLine(int x, int y, int w, uint c) => FillRect(x, y, w, 1, c);
    public static void VLine(int x, int y, int h, uint c) => FillRect(x, y, 1, h, c);

    public static void Line(int x0, int y0, int x1, int y1, uint c, int thick = 1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            if (thick <= 1) Px(x0, y0, c);
            else FillRect(x0, y0, thick, thick, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public static void Circle(int cx, int cy, int r, uint c)
    {
        for (int yy = -r; yy <= r; yy++)
            for (int xx = -r; xx <= r; xx++)
                if (xx * xx + yy * yy <= r * r)
                    Px(cx + xx, cy + yy, c);
    }

    public static void Text(int x, int y, string s, uint c, int scale = 1)
    {
        int cx = x;
        foreach (char ch in s)
        {
            if (ch != ' ') Font.Draw(cx, y, ch, c, scale);
            cx += 6 * scale;
        }
    }

    public static void TextCentered(int y, string s, uint c, int scale = 1)
        => Text((W - s.Length * 6 * scale) / 2, y, s, c, scale);

    /// <summary>Draw a tiny sprite authored as rows of chars. '.'=transparent, others looked up in palette.</summary>
    public static void Sprite(int x, int y, string[] rows, Func<char, uint> pal, bool flip = false)
    {
        for (int ry = 0; ry < rows.Length; ry++)
        {
            string row = rows[ry];
            for (int rx = 0; rx < row.Length; rx++)
            {
                char ch = flip ? row[row.Length - 1 - rx] : row[rx];
                if (ch == '.') continue;
                Px(x + rx, y + ry, pal(ch));
            }
        }
    }
}
