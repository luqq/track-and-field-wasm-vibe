namespace TrackAndField;

/// <summary>Top-level state machine: title, event intros, the six-event "Match" loop, results, game over.</summary>
public static class Game
{
    public static Random Rng = new(1983);

    private enum Mode { Title, Intro, Play, Result, GameOver }
    private static Mode _mode = Mode.Title;
    private static double _modeT;

    private static int _match;                // 1-based
    private static int _eventIndex;
    private static long _score, _hiScore = 30000;
    private static int _lives;
    private static long _nextLifeAt;

    private static readonly EventBase[] Events =
    {
        new Dash100(), new LongJump(), new Javelin(),
        new Hurdles110(), new Hammer(), new HighJump(),
    };
    private static EventBase Current => Events[_eventIndex];

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
        _score = Math.Min(9_999_990, _score + pts);
        if (_score >= _nextLifeAt)
        {
            _lives++;
            _nextLifeAt += 100_000;
            Audio.Jingle(Audio.JingleRecord);
        }
        if (_score > _hiScore) _hiScore = _score;
    }

    public static void SeedFrom(double timestamp) => Rng = new Random((int)timestamp ^ 0x5EED);

    public static void Step()
    {
        Input.BeginStep();
        _modeT += 1.0 / 60.0;

        switch (_mode)
        {
            case Mode.Title:
                _titleAnim += 1.0 / 60.0;
                if (Input.StartPressed || Input.ActionPressed) StartGame();
                break;

            case Mode.Intro:
                if (_modeT > 2.4 || Input.ActionPressed || Input.StartPressed)
                {
                    _mode = Mode.Play; _modeT = 0;
                }
                break;

            case Mode.Play:
                Current.Step();
                if (Current.Finished)
                {
                    if (Current.Qualified) AddScore(Current.Points);
                    _mode = Mode.Result; _modeT = 0;
                    Audio.Jingle(Current.Qualified ? Audio.JingleQualify : Audio.JingleFail);
                }
                break;

            case Mode.Result:
                if (_modeT > 3.0)
                {
                    if (Current.Qualified) NextEvent();
                    else if (_lives > 0) { _lives--; Current.Reset(MatchLevel()); _mode = Mode.Intro; _modeT = 0; }
                    else { _mode = Mode.GameOver; _modeT = 0; }
                }
                break;

            case Mode.GameOver:
                if (_modeT > 4.0 || Input.StartPressed) { _mode = Mode.Title; _modeT = 0; }
                break;
        }

        if (_eggT >= 0)
        {
            _eggT += 1.0 / 60.0;
            if (_eggT > 3.0) _eggT = -1;
        }
    }

    private static int MatchLevel() => Math.Min(_match, 3); // difficulty locks at match 3 forever

    private static void StartGame()
    {
        _match = 1; _eventIndex = 0;
        _score = 0; _lives = 0; _nextLifeAt = 100_000;
        Current.Reset(MatchLevel());
        _mode = Mode.Intro; _modeT = 0;
        Audio.Jingle(Audio.JingleTitle);
    }

    private static void NextEvent()
    {
        _eventIndex++;
        if (_eventIndex >= Events.Length)
        {
            _eventIndex = 0;
            _match++; // survived the Match: same events, harsher marks
        }
        Current.Reset(MatchLevel());
        _mode = Mode.Intro; _modeT = 0;
    }

    public static void Draw()
    {
        switch (_mode)
        {
            case Mode.Title: DrawTitle(); break;

            case Mode.Intro:
                Gfx.Clear(Gfx.Black);
                Gfx.TextCentered(60, $"MATCH {_match}", Gfx.Cyan);
                Gfx.TextCentered(84, Current.Name, Gfx.White, 2);
                Gfx.TextCentered(112, Current.QualText, Gfx.Yellow);
                Gfx.TextCentered(140, EventHint(), Gfx.Gray);
                DrawHud();
                break;

            case Mode.Play:
                Current.Draw();
                DrawHud();
                break;

            case Mode.Result:
                Current.Draw();
                Gfx.FillRect(28, 78, 200, 60, Gfx.Black);
                Gfx.TextCentered(84, Current.Qualified ? "QUALIFIED!" : "NOT QUALIFIED", Current.Qualified ? Gfx.Yellow : Gfx.Red, 2);
                Gfx.TextCentered(104, Current.ResultText, Gfx.White);
                if (Current.Qualified) Gfx.TextCentered(116, $"BONUS {Current.Points} PTS", Gfx.Cyan);
                else if (_lives > 0) Gfx.TextCentered(116, "EXTRA LIFE - TRY AGAIN", Gfx.Cyan);
                DrawHud();
                break;

            case Mode.GameOver:
                Gfx.Clear(Gfx.Black);
                Gfx.TextCentered(80, "GAME OVER", Gfx.Red, 2);
                Gfx.TextCentered(110, $"SCORE {_score}", Gfx.White);
                Gfx.TextCentered(122, $"HI    {_hiScore}", Gfx.Yellow);
                break;
        }

        DrawEggOverlay();
    }

    private static string EventHint() => Current switch
    {
        Dash100 => "MASH RUN AFTER THE GUN!",
        LongJump => "RUN - HOLD ACTION - AIM 45~",
        Javelin => "RUN - HOLD ACTION - AIM 43~",
        Hurdles110 => "MASH RUN - ACTION TO JUMP",
        Hammer => "TAP RUN ONCE - ACTION AT 45~",
        HighJump => "HOLD ACTION AT THE BAR",
        _ => "",
    };

    private static void DrawHud()
    {
        Gfx.Text(8, Gfx.H - 8, $"{_score:0000000}", Gfx.White);
        Gfx.Text(100, Gfx.H - 8, $"HI {_hiScore:0000000}", Gfx.Yellow);
        Gfx.Text(210, Gfx.H - 8, $"LIFE {_lives}", Gfx.Cyan);
    }

    private static void DrawTitle()
    {
        Gfx.Clear(Gfx.Black);
        Scene.DrawStadium((int)(_titleAnim * 30));
        Gfx.FillRect(0, 88, Gfx.W, Gfx.H - 88, Gfx.TrackRed);
        Gfx.HLine(0, 126, Gfx.W, Gfx.White);
        Gfx.HLine(0, 162, Gfx.W, Gfx.White);
        Gfx.HLine(0, 194, Gfx.W, Gfx.White);

        Gfx.TextCentered(36, "TRACK + FIELD", Gfx.Yellow, 3);
        Gfx.TextCentered(66, "KONAMI 1983 - WASM TRIBUTE", Gfx.White);

        // demo runner loops across the track
        double ph = _titleAnim * 10;
        int dx = (int)(_titleAnim * 120) % (Gfx.W + 60) - 30;
        Athlete.Run(dx, 190, ph, 0.8, Gfx.Red);
        Athlete.Run((dx + 400) % (Gfx.W + 60) - 30, 158, ph + 1.5, 0.8, Gfx.Blue);

        if ((int)(_titleAnim * 2) % 2 == 0)
            Gfx.TextCentered(206, "PUSH ACTION TO START", Gfx.White);
        Gfx.Text(4, Gfx.H - 8, "RUN: Z X / ARROWS  ACTION: SPACE", Gfx.Gray);
    }

    private static void DrawEggOverlay()
    {
        if (_eggT < 0) return;
        // Tutankham explorer scuttles along the bottom carrying the key
        int x = (int)(_eggT / 3.0 * (Gfx.W + 40)) - 20;
        Gfx.Sprite(x, Gfx.H - 26, Props.Tut, Props.Pal, false);
        Gfx.Sprite(Gfx.W - x - 8, Gfx.H - 44, Props.Mole, Props.Pal, false);
        if (_eggT is > 0.3 and < 2.6)
            Gfx.TextCentered(58, "SECRET! +1000 PTS", (int)(_eggT * 8) % 2 == 0 ? Gfx.Yellow : Gfx.White);
    }

    /// <summary>Bird easter egg support for the javelin: drawn by the overlay too.</summary>
    public static void DrawBird(int x, int y) => Gfx.Sprite(x, y, Props.Bird, Props.Pal, false);
}
