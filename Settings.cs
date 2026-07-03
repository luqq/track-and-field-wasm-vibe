namespace TrackAndField;

/// <summary>
/// Player-facing settings: difficulty, language, voice announcer and input bindings.
/// Persisted to localStorage as a small pipe-delimited string (no reflection, AOT-safe).
/// </summary>
public static class Settings
{
    public const string StorageKey = "prankyield.settings";

    public static int Difficulty = 1;   // 0 easy, 1 normal, 2 hard
    public static int Lang = 0;         // 0 es, 1 en, 2 ca
    public static bool VoiceOn = true;

    /// <summary>Raw input code (KeyboardEvent.code or "PAD0".."PAD15") -> button 0..3.</summary>
    public static Dictionary<string, int> Bindings = DefaultBindings();

    public static Dictionary<string, int> DefaultBindings() => new()
    {
        ["KeyZ"] = Input.RunA, ["ArrowLeft"] = Input.RunA,
        ["KeyX"] = Input.RunB, ["ArrowRight"] = Input.RunB,
        ["Space"] = Input.Action, ["ArrowUp"] = Input.Action,
        ["Enter"] = Input.Start,
        // gamepad: A/B mash to run, X/RB action, Start
        ["PAD0"] = Input.RunA, ["PAD1"] = Input.RunB,
        ["PAD2"] = Input.Action, ["PAD5"] = Input.Action,
        ["PAD9"] = Input.Start,
    };

    /// <summary>Replace every code bound to an action with a single new code.</summary>
    public static void Rebind(int action, string code)
    {
        foreach (var k in Bindings.Where(kv => kv.Value == action).Select(kv => kv.Key).ToList())
            Bindings.Remove(k);
        Bindings[code] = action;
    }

    // --- difficulty knobs (see TUNING.md) ---
    public static double TimeF => Difficulty switch { 0 => 1.12, 2 => 0.94, _ => 1.0 };  // time quals
    public static double DistF => Difficulty switch { 0 => 0.85, 2 => 1.08, _ => 1.0 };  // distance quals
    public static double TapF => Difficulty switch { 0 => 1.25, 2 => 0.88, _ => 1.0 };   // RunMeter gain

    public static void Save()
    {
        string binds = string.Join(";", Bindings.Select(kv => $"{kv.Key}:{kv.Value}"));
        try { Audio.StorageSet(StorageKey, $"v1|{Difficulty}|{Lang}|{(VoiceOn ? 1 : 0)}|{binds}"); }
        catch { /* storage unavailable (private mode, etc.) */ }
    }

    public static void Load()
    {
        string? raw;
        try { raw = Audio.StorageGet(StorageKey); }
        catch { return; }
        if (string.IsNullOrEmpty(raw)) return;
        var parts = raw.Split('|');
        if (parts.Length < 5 || parts[0] != "v1") return;
        if (int.TryParse(parts[1], out int d)) Difficulty = Math.Clamp(d, 0, 2);
        if (int.TryParse(parts[2], out int l)) Lang = Math.Clamp(l, 0, 2);
        VoiceOn = parts[3] == "1";
        if (parts[4].Length > 0)
        {
            var b = new Dictionary<string, int>();
            foreach (var pair in parts[4].Split(';'))
            {
                int c = pair.LastIndexOf(':');
                if (c > 0 && int.TryParse(pair[(c + 1)..], out int btn) && btn is >= 0 and <= 3)
                    b[pair[..c]] = btn;
            }
            if (b.Count > 0) Bindings = b;
        }
    }
}
