namespace TrackAndField;

/// <summary>
/// Top-level state machine: title menu, options (difficulty / language / voice / keys),
/// alternating 1-2 player turns over the six-event "Match" loop, results, game over.
/// </summary>
public static class Game
{
    public static Random Rng = new(1983);

    private enum Mode { Title, Options, Remap, Intro, Play, Result, GameOver }
    private static Mode _mode = Mode.Title;
    private static double _modeT;

    private class PlayerState
    {
        public long Score;
        public int Lives;
        public long NextLife = 100_000;
        public bool Out;
        public uint Jersey;
    }

    private static readonly PlayerState[] Players = { new(), new() };
    private static int _playerCount = 1;
    private static int _cur;
    private static readonly bool[] _played = new bool[2];
    private static long _hiScore = 30000;

    /// <summary>Jersey color of the player currently on the field (P1 red, P2 green).</summary>
    public static uint CurJersey => Players[_cur].Jersey;

    private static int _match;
    private static int _eventIndex;

    private static readonly EventBase[] Events =
    {
        new Dash100(), new LongJump(), new Javelin(),
        new Hurdles110(), new Hammer(), new HighJump(),
    };
    private static EventBase Current => Events[_eventIndex];

    // menus
    private static int _menuSel;
    private static int _optSel;
    private static int _remapIdx;

    // easter-egg overlay (pub/sub style: events publish, the overlay subscribes)
    private static double _eggT = -1;
    private static double _titleAnim;

    public static void TriggerEgg()
    {
        _eggT = 0;
        AddScore(1000);
        Audio.Jingle(Audio.JingleEgg);
    }

    private static void AddScore(long pts)
    {
        var p = Players[_cur];
        p.Score = Math.Min(9_999_990, p.Score + pts);
        if (p.Score >= p.NextLife)
        {
            p.Lives++;
            p.NextLife += 100_000;
            Audio.Jingle(Audio.JingleRecord);
        }
        if (p.Score > _hiScore) _hiScore = p.Score;
    }

    public static void SeedFrom(double timestamp) => Rng = new Random((int)timestamp ^ 0x5EED);

    public static void Step()
    {
        Input.BeginStep();
        _modeT += 1.0 / 60.0;

        switch (_mode)
        {
            case Mode.Title: StepTitle(); break;
            case Mode.Options: StepOptions(); break;
            case Mode.Remap: StepRemap(); break;

            case Mode.Intro:
                if (_modeT > 2.4 || Input.ActionPressed || Input.StartPressed)
                {
                    _mode = Mode.Play; _modeT = 0;
                    Audio.Whistle(); // referee whistle opens the event
                }
                break;

            case Mode.Play:
                Current.Step();
                if (Current.Finished)
                {
                    if (Current.Qualified) AddScore(Current.Points);
                    _mode = Mode.Result; _modeT = 0;
                    Audio.Jingle(Current.Qualified ? Audio.JingleQualify : Audio.JingleFail);
                    Voice.Qualified(Current.Qualified);
                }
                break;

            case Mode.Result:
                if (_modeT > 3.0) AfterResult();
                break;

            case Mode.GameOver:
                if (_modeT > 4.0 || Input.StartPressed || Input.ActionPressed)
                {
                    _mode = Mode.Title; _modeT = 0;
                }
                break;
        }

        if (_eggT >= 0)
        {
            _eggT += 1.0 / 60.0;
            if (_eggT > 3.0) _eggT = -1;
        }
    }

    // ---------------------------------------------------------------- menus
    private static void StepTitle()
    {
        _titleAnim += 1.0 / 60.0;
        if (Input.RunTaps > 0)
        {
            _menuSel = (_menuSel + Input.RunTaps) % 3;
            Audio.Tick(0.4);
        }
        if (Input.ActionPressed || Input.StartPressed)
        {
            switch (_menuSel)
            {
                case 0: StartGame(1); break;
                case 1: StartGame(2); break;
                case 2: _mode = Mode.Options; _optSel = 0; _modeT = 0; Audio.Beep(); break;
            }
        }
    }

    private static void StepOptions()
    {
        const int items = 6; // diff, lang, voice, remap, defaults, back
        if (Input.RunTaps > 0)
        {
            _optSel = (_optSel + Input.RunTaps) % items;
            Audio.Tick(0.4);
        }
        if (!Input.ActionPressed && !Input.StartPressed) return;

        switch (_optSel)
        {
            case 0:
                Settings.Difficulty = (Settings.Difficulty + 1) % 3;
                Audio.Beep();
                break;
            case 1:
                Settings.Lang = (Settings.Lang + 1) % 3;
                Audio.Beep();
                break;
            case 2:
                Settings.VoiceOn = !Settings.VoiceOn;
                Audio.Beep();
                Voice.Qualified(true); // audible sample of the announcer
                break;
            case 3:
                _mode = Mode.Remap; _remapIdx = 0; _modeT = 0;
                Input.Capturing = true; Input.CapturedCode = null;
                break;
            case 4:
                Settings.Bindings = Settings.DefaultBindings();
                Settings.Save();
                Audio.Jingle(Audio.JingleQualify);
                break;
            default:
                Settings.Save();
                _mode = Mode.Title; _modeT = 0;
                Audio.Beep();
                break;
        }
    }

    private static void StepRemap()
    {
        if (Input.CapturedCode is not string code) return;
        Input.CapturedCode = null;
        Settings.Rebind(_remapIdx, code);
        Audio.Tick(0.3 + _remapIdx * 0.2);
        _remapIdx++;
        if (_remapIdx >= 4)
        {
            Input.Capturing = false;
            Input.ClearState();
            Settings.Save();
            _mode = Mode.Options; _modeT = 0;
            Audio.Jingle(Audio.JingleQualify);
        }
    }

    // ------------------------------------------------------------- game flow
    private static int MatchLevel() => Math.Min(_match, 3); // difficulty locks at match 3 forever

    private static void StartGame(int players)
    {
        _playerCount = players;
        _match = 1; _eventIndex = 0; _cur = 0;
        for (int i = 0; i < 2; i++)
        {
            Players[i].Score = 0; Players[i].Lives = 0;
            Players[i].NextLife = 100_000; Players[i].Out = i >= players;
        }
        Players[0].Jersey = Gfx.Red;
        Players[1].Jersey = Gfx.Rgb(0, 190, 80);
        ResetTurnFlags();
        Current.Reset(MatchLevel());
        EnterIntro();
    }

    private static void ResetTurnFlags()
    {
        for (int i = 0; i < 2; i++) _played[i] = Players[i].Out;
        int first = Array.IndexOf(_played, false);
        _cur = first >= 0 ? first : 0;
    }

    private static void AfterResult()
    {
        var p = Players[_cur];
        if (!Current.Qualified)
        {
            if (p.Lives > 0)
            {
                p.Lives--;
                Current.Reset(MatchLevel());
                EnterIntro();
                return; // same player retries the same event
            }
            p.Out = true;
        }
        _played[_cur] = true;

        int next = -1;
        for (int i = 0; i < _playerCount; i++)
            if (!_played[i] && !Players[i].Out) next = i;
        if (next >= 0)
        {
            _cur = next;
            Current.Reset(MatchLevel());
            EnterIntro();
            return;
        }

        bool anyAlive = false;
        for (int i = 0; i < _playerCount; i++) anyAlive |= !Players[i].Out;
        if (!anyAlive) { _mode = Mode.GameOver; _modeT = 0; return; }

        NextEvent();
    }

    private static void NextEvent()
    {
        _eventIndex++;
        if (_eventIndex >= Events.Length)
        {
            _eventIndex = 0;
            _match++; // survived the Match: same events, harsher marks
        }
        ResetTurnFlags();
        Current.Reset(MatchLevel());
        EnterIntro();
    }

    /// <summary>Every event is announced with the pre-event fanfare.</summary>
    private static void EnterIntro()
    {
        _mode = Mode.Intro; _modeT = 0;
        Audio.Jingle(Audio.JingleFanfare);
    }

    // ------------------------------------------------------------------ draw
    public static void Draw()
    {
        switch (_mode)
        {
            case Mode.Title: DrawTitle(); break;
            case Mode.Options: DrawOptions(); break;
            case Mode.Remap: DrawRemap(); break;

            case Mode.Intro:
                Gfx.Clear(Gfx.Black);
                Gfx.TextCentered(52, $"MATCH {_match}", Gfx.Cyan);
                if (_playerCount == 2)
                    Gfx.TextCentered(64, $"{L.Player} {_cur + 1}", CurJersey);
                int sc = Current.Name.Length * 12 <= 250 ? 2 : 1;
                Gfx.TextCentered(sc == 2 ? 84 : 88, Current.Name, Gfx.White, sc);
                Gfx.TextCentered(112, Current.QualText, Gfx.Yellow);
                Gfx.TextCentered(140, L.EventHints[_eventIndex], Gfx.Gray);
                DrawHud();
                break;

            case Mode.Play:
                Current.Draw();
                DrawHud();
                break;

            case Mode.Result:
                Current.Draw();
                Gfx.FillRect(24, 78, 208, 60, Gfx.Black);
                Gfx.TextCentered(84, Current.Qualified ? L.Qualified : L.NotQualified, Current.Qualified ? Gfx.Yellow : Gfx.Red, 2);
                Gfx.TextCentered(104, Current.ResultText, Gfx.White);
                if (Current.Qualified) Gfx.TextCentered(116, $"{L.Bonus} {Current.Points} PTS", Gfx.Cyan);
                else if (Players[_cur].Lives > 0) Gfx.TextCentered(116, L.ExtraLife, Gfx.Cyan);
                DrawHud();
                break;

            case Mode.GameOver:
                Gfx.Clear(Gfx.Black);
                Gfx.TextCentered(70, L.GameOver, Gfx.Red, 2);
                if (_playerCount == 2)
                {
                    Gfx.TextCentered(100, $"P1 {Players[0].Score:0000000}", Players[0].Jersey);
                    Gfx.TextCentered(112, $"P2 {Players[1].Score:0000000}", Players[1].Jersey);
                }
                else
                {
                    Gfx.TextCentered(104, $"{Players[0].Score:0000000}", Gfx.White);
                }
                Gfx.TextCentered(130, $"HI {_hiScore:0000000}", Gfx.Yellow);
                break;
        }

        DrawEggOverlay();
    }

    private static void DrawHud()
    {
        if (_playerCount == 2)
        {
            Gfx.Text(4, Gfx.H - 8, $"P1 {Players[0].Score:0000000}", _cur == 0 ? Players[0].Jersey : Gfx.Gray);
            Gfx.Text(90, Gfx.H - 8, $"P2 {Players[1].Score:0000000}", _cur == 1 ? Players[1].Jersey : Gfx.Gray);
            Gfx.Text(180, Gfx.H - 8, $"V{Players[_cur].Lives} M{_match}", Gfx.Cyan);
        }
        else
        {
            Gfx.Text(8, Gfx.H - 8, $"{Players[0].Score:0000000}", Gfx.White);
            Gfx.Text(100, Gfx.H - 8, $"HI {_hiScore:0000000}", Gfx.Yellow);
            Gfx.Text(210, Gfx.H - 8, $"V {Players[0].Lives}", Gfx.Cyan);
        }
    }

    private static void DrawTitle()
    {
        // 1983 attract-mode look: black sky, red desert mesas, green track
        Gfx.Clear(Gfx.Black);

        // stacked stars-and-stripes logo with yellow drop shadow
        Gfx.StripedText(46, 14, "PRANK", 4);
        Gfx.StripedText(88, 50, "+YIELD", 4);

        string[] items = { L.OnePlayer, L.TwoPlayers, L.Options };
        for (int i = 0; i < items.Length; i++)
        {
            uint c = i == _menuSel ? Gfx.Yellow : Gfx.White;
            string pre = i == _menuSel ? "> " : "  ";
            Gfx.TextCentered(96 + i * 11, pre + items[i], c);
        }
        Gfx.TextCentered(130, L.MenuHint, Gfx.Gray);

        // monument-valley mesas on the horizon
        Gfx.FillRect(52, 152, 46, 8, Gfx.Red);
        Gfx.FillRect(60, 144, 30, 8, Gfx.Red);
        Gfx.FillRect(70, 138, 12, 6, Gfx.Red);
        Gfx.FillRect(158, 158, 30, 4, Gfx.Red);
        Gfx.FillRect(164, 152, 18, 6, Gfx.Red);
        Gfx.FillRect(214, 160, 20, 2, Gfx.Red);

        // green track with lane scanlines
        Gfx.FillRect(0, 162, Gfx.W, 54, Gfx.Grass);
        for (int y = 164; y < 216; y += 5) Gfx.HLine(0, y, Gfx.W, Gfx.GrassDark);

        // four demo runners chase each other across the track
        double ph = _titleAnim * 10;
        int dx = (int)(_titleAnim * 120) % (Gfx.W + 120) - 60;
        uint[] jerseys = { Gfx.Rgb(255, 140, 0), Gfx.Cyan, Gfx.Red, Gfx.White };
        for (int i = 0; i < 4; i++)
            Athlete.Run(dx - i * 22, 206, ph + i * 1.4, 0.85, jerseys[i]);

        Gfx.TextCentered(216, "(C) KONAMI 1983 - WASM TRIBUTE", Gfx.White);
    }

    private static void DrawOptions()
    {
        Gfx.Clear(Gfx.Black);
        Gfx.TextCentered(28, L.Options, Gfx.Yellow, 2);

        string[] rows =
        {
            $"{L.Difficulty}: {L.DiffNames[Settings.Difficulty]}",
            $"{L.Language}: {L.LangNames[Settings.Lang]}",
            $"{L.VoiceLbl}: {(Settings.VoiceOn ? L.On : L.Off)}",
            L.Keys,
            L.KeysDefault,
            L.Back,
        };
        for (int i = 0; i < rows.Length; i++)
        {
            uint c = i == _optSel ? Gfx.Yellow : Gfx.White;
            string pre = i == _optSel ? "> " : "  ";
            Gfx.Text(28, 64 + i * 14, pre + rows[i], c);
        }
        Gfx.TextCentered(196, L.MenuHint, Gfx.Gray);
    }

    private static void DrawRemap()
    {
        Gfx.Clear(Gfx.Black);
        Gfx.TextCentered(40, L.Keys, Gfx.Yellow, 2);
        Gfx.TextCentered(84, L.PressKeyFor, Gfx.White);
        Gfx.TextCentered(100, L.ActionNames[_remapIdx], Gfx.Cyan, 2);
        // already assigned
        for (int i = 0; i < _remapIdx; i++)
        {
            string code = "?";
            foreach (var kv in Settings.Bindings) if (kv.Value == i) code = kv.Key;
            Gfx.Text(48, 140 + i * 10, $"{L.ActionNames[i]}: {code.ToUpperInvariant()}", Gfx.Gray);
        }
    }

    private static void DrawEggOverlay()
    {
        if (_eggT < 0) return;
        // Tutankham explorer scuttles along the bottom carrying the key
        int x = (int)(_eggT / 3.0 * (Gfx.W + 40)) - 20;
        Gfx.Sprite(x, Gfx.H - 26, Props.Tut, Props.Pal, false);
        Gfx.Sprite(Gfx.W - x - 8, Gfx.H - 44, Props.Mole, Props.Pal, false);
        if (_eggT is > 0.3 and < 2.6)
            Gfx.TextCentered(58, L.Secret, (int)(_eggT * 8) % 2 == 0 ? Gfx.Yellow : Gfx.White);
    }

    /// <summary>Bird easter egg support for the javelin: drawn by the overlay too.</summary>
    public static void DrawBird(int x, int y) => Gfx.Sprite(x, y, Props.Bird, Props.Pal, false);
}
