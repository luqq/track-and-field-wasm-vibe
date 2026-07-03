namespace TrackAndField;

/// <summary>
/// Long Jump. Hold ACTION near the board to raise the angle, release to jump.
/// Hitting the white board line injects a colossal inertia bonus; the 45/44/43 degree
/// bonus table applies fully here (unlike the javelin).
/// </summary>
public class LongJump : EventBase
{
    public override string Name => L.EventNames[1];
    public override string QualText => $"{L.Qualify} {_qual:0.00} M";

    private const int FoulCm = 4500;
    private enum Ph { Wait, Run, Hold, Fly, Land, Foul, Done }
    private Ph _ph;
    private double _qual, _phT;
    private readonly RunMeter _run = new();
    private double _posCm, _anim, _angle;
    private double _fx, _fy, _fvx, _fvy;   // flight state (cm)
    private int _attempt;
    private readonly double[] _marks = new double[3];
    private int _sameMarkStreak; private int _lastMarkCm = -1;
    private double _best;

    public override void Reset(int match)
    {
        _qual = Math.Round((match switch { 1 => 6.50, 2 => 8.50, _ => 9.00 }) * Settings.DistF, 2);
        _run.TapGain = 55 * Settings.TapF;
        _attempt = 0; _best = 0; Array.Clear(_marks);
        _sameMarkStreak = 0; _lastMarkCm = -1;
        Finished = Qualified = false; Points = 0; ResultText = "";
        NextAttempt();
    }

    private void NextAttempt()
    {
        _ph = Ph.Wait; _phT = 1.2;
        _run.Reset();
        _posCm = _anim = _angle = 0;
    }

    public override void Step()
    {
        switch (_ph)
        {
            case Ph.Wait:
                _phT -= Dt;
                if (_phT <= 0) { _ph = Ph.Run; Audio.Beep(); }
                break;

            case Ph.Run:
                _run.Step(Input.RunTaps);
                _posCm += _run.SpeedCms * Dt;
                _anim += _run.SpeedCms * Dt * 0.02;
                if (Input.ActionPressed) { _ph = Ph.Hold; _angle = 0; }
                else if (_posCm >= FoulCm) Foul();
                break;

            case Ph.Hold:
                // planted for takeoff: the athlete stops dead while the angle climbs,
                // but friction keeps draining the speed he built up
                _run.Step(0);
                _angle = Math.Min(90, _angle + 1.5);
                if (Input.ActionReleased || _angle >= 90) Launch();
                break;

            case Ph.Fly:
                _fvy -= 981 * Dt;
                _fx += _fvx * Dt;
                _fy += _fvy * Dt;
                if (_fy <= 0) LandNow();
                break;

            case Ph.Land:
            case Ph.Foul:
                _phT -= Dt;
                if (_phT <= 0)
                {
                    if (_attempt >= 3) FinishEvent();
                    else NextAttempt();
                }
                break;

            case Ph.Done:
                _phT -= Dt;
                if (_phT <= 0) Finished = true;
                break;
        }
    }

    private void Foul()
    {
        _attempt++;
        _marks[_attempt - 1] = -1;
        _ph = Ph.Foul; _phT = 1.8;
        Voice.Foul();
        _sameMarkStreak = 0; _lastMarkCm = -1;
        Audio.Jingle(Audio.JingleFail);
    }

    private void Launch()
    {
        int deg = (int)Math.Round(_angle);
        double gapCm = FoulCm - _posCm;
        double bonus = AngleBonusCms(deg);              // full table: works in this event
        if (gapCm <= 15) bonus += 100;                  // toe on the white line: colossal boost
        double vEff = 600 + 0.55 * (_run.SpeedCms + bonus);
        double rad = Math.Max(1, deg) * Math.PI / 180.0;
        // 0.5 range factor folded into vx so the visual flight matches the measured mark
        _fvx = vEff * Math.Cos(rad) * 0.5;
        _fvy = vEff * Math.Sin(rad);
        _fx = _posCm; _fy = 1;
        _ph = Ph.Fly;
        Audio.Tone(600, 80, 0.2);
    }

    private void LandNow()
    {
        _attempt++;
        double meters = (_fx - FoulCm) / 100.0;
        if (meters < 0) meters = 0;
        int cmMark = (int)Math.Round(meters * 100);
        _marks[_attempt - 1] = cmMark / 100.0;
        if (_marks[_attempt - 1] > _best) _best = _marks[_attempt - 1];

        // easter egg: identical mark three times in a row
        if (cmMark > 0 && cmMark == _lastMarkCm) _sameMarkStreak++;
        else _sameMarkStreak = 1;
        _lastMarkCm = cmMark;
        if (_sameMarkStreak >= 3) Game.TriggerEgg();

        _ph = Ph.Land; _phT = 2.0;
        Voice.Meters(_marks[_attempt - 1]);
        Audio.Tone(400, 100, 0.2);
    }

    private void FinishEvent()
    {
        Qualified = _best >= _qual;
        Points = Qualified ? (int)((_best - _qual) * 2000 + 1000) / 10 * 10 : 0;
        ResultText = _best > 0 ? $"{L.Best} {_best:0.00} M" : L.NoMark;
        _ph = Ph.Done; _phT = 2.2;
        Audio.Jingle(Qualified ? Audio.JingleQualify : Audio.JingleFail);
    }

    public override void Draw()
    {
        double focus = _ph == Ph.Fly || _ph == Ph.Land ? _fx : _posCm;
        int cam = Math.Max(0, (int)(focus * Scene.PxPerCm) - 70);
        Scene.DrawStadium(cam);
        Scene.DrawField(cam, FoulCm, 12);
        // sand pit
        int pit = (int)(FoulCm * Scene.PxPerCm) - cam;
        Gfx.FillRect(pit + 3, Scene.GroundY - 3, (int)(1100 * Scene.PxPerCm), 10, Gfx.Sand);

        switch (_ph)
        {
            case Ph.Wait:
            case Ph.Run:
                int px = (int)(_posCm * Scene.PxPerCm) - cam;
                if (_run.SpeedCms > 1)
                    Athlete.Run(px, Scene.GroundY, _anim * Math.PI, _run.SpeedCms / 1400, Game.CurJersey);
                else Athlete.Crouch(px, Scene.GroundY, Game.CurJersey);
                break;
            case Ph.Hold:
                Athlete.Crouch((int)(_posCm * Scene.PxPerCm) - cam, Scene.GroundY, Game.CurJersey);
                Scene.AngleMeter(_angle);
                break;
            case Ph.Fly:
                int fx = (int)(_fx * Scene.PxPerCm) - cam;
                int fy = Scene.GroundY - (int)(_fy * 0.28);
                Athlete.Fly(fx, fy, Game.CurJersey, 55);
                Scene.AngleMeter(_angle);
                break;
            case Ph.Land:
                int lx = (int)(_fx * Scene.PxPerCm) - cam;
                Athlete.Crouch(lx, Scene.GroundY, Game.CurJersey);
                Gfx.TextCentered(96, $"{_marks[_attempt - 1]:0.00} M", Gfx.White, 2);
                break;
            case Ph.Foul:
                Gfx.TextCentered(96, L.Foul, Gfx.Red, 2);
                break;
            case Ph.Done:
                Gfx.TextCentered(96, ResultText, Gfx.White, 2);
                break;
        }

        Gfx.Text(8, 4, $"{L.Attempt} {Math.Min(_attempt + (_ph is Ph.Land or Ph.Foul or Ph.Done ? 0 : 1), 3)}/3", Gfx.White);
        Gfx.Text(130, 4, $"{L.Qual} {_qual:0.00}M", Gfx.Cyan);
        Gfx.Text(8, 12, $"{L.Best} {_best:0.00}M", Gfx.Yellow);
        if (_ph is Ph.Run or Ph.Wait or Ph.Hold) Scene.SpeedBar(_run.SpeedCms);
    }
}

/// <summary>
/// Javelin Throw. Carrier speed hard-capped at 1300 cm/s, fixed +330 cm/s arm thrust,
/// previous-attempt distance carryover, the corrupted X-register (NO 45 degree bonus),
/// the >99.99 m rollover bug, and mid-flight tailwind taps.
/// </summary>
public class Javelin : EventBase
{
    public override string Name => L.EventNames[2];
    public override string QualText => $"{L.Qualify} {_qual:0.00} M";

    private const int FoulCm = 4000;
    private const double GEff = 981.0 / 3.2; // arcade-scaled gravity
    private enum Ph { Wait, Run, Hold, Throwing, Fly, Land, Foul, Egg, Done }
    private Ph _ph;
    private double _qual, _phT;
    private readonly RunMeter _run = new();
    private double _posCm, _anim, _angle, _throwT;
    private double _jx, _jy, _jvx, _jvy;
    private double _carrySpeed;
    private int _attempt;
    private readonly double[] _marks = new double[3];
    private double _best, _prevMark;
    private bool _rolledOver;

    public override void Reset(int match)
    {
        _qual = Math.Round((match switch { 1 => 70.00, 2 => 75.00, _ => 80.00 }) * Settings.DistF, 2);
        // generous tap gain: on the original board reaching the 1300 cm/s cap was the
        // easy part (hence the cap) — the skill lives in the release angle
        _run.TapGain = 78.0 * Settings.TapF;
        _attempt = 0; _best = 0; _prevMark = 0; Array.Clear(_marks);
        Finished = Qualified = false; Points = 0; ResultText = "";
        NextAttempt();
    }

    private void NextAttempt()
    {
        _ph = Ph.Wait; _phT = 1.2;
        _run.Reset();
        _posCm = _anim = _angle = _throwT = 0;
        _rolledOver = false;
    }

    public override void Step()
    {
        switch (_ph)
        {
            case Ph.Wait:
                _phT -= Dt;
                if (_phT <= 0) { _ph = Ph.Run; Audio.Beep(); }
                break;

            case Ph.Run:
                _run.Step(Input.RunTaps);
                _posCm += _run.SpeedCms * Dt;
                _anim += _run.SpeedCms * Dt * 0.02;
                if (Input.ActionPressed) { _ph = Ph.Hold; _angle = 0; }
                else if (_posCm >= FoulCm) Foul();
                break;

            case Ph.Hold:
                _run.Step(0);
                _posCm += _run.SpeedCms * Dt * 0.35; // planting slows the athlete
                _angle = Math.Min(88, _angle + 1.2); // slower climb = finer aim control
                if (_posCm >= FoulCm) { Foul(); return; }
                if (Input.ActionReleased || _angle >= 88) BeginThrow();
                break;

            case Ph.Throwing:
                _throwT += Dt;
                if (_throwT >= 0.22) Release();
                break;

            case Ph.Fly:
                // frantic run taps keep a faint programmatic tailwind alive
                _jvx += Input.RunTaps * 1.5;
                _jvy -= GEff * Dt;
                _jx += _jvx * Dt;
                _jy += _jvy * Dt;
                if (_jy <= 0) LandNow();
                break;

            case Ph.Egg:
                _phT -= Dt;
                if (_phT <= 0) EndAttemptAsFoul();
                break;

            case Ph.Land:
            case Ph.Foul:
                _phT -= Dt;
                if (_phT <= 0)
                {
                    if (_attempt >= 3) FinishEvent();
                    else NextAttempt();
                }
                break;

            case Ph.Done:
                _phT -= Dt;
                if (_phT <= 0) Finished = true;
                break;
        }
    }

    private void Foul()
    {
        _attempt++;
        _marks[_attempt - 1] = -1;
        _prevMark = 0;
        _ph = Ph.Foul; _phT = 1.8;
        Voice.Foul();
        Audio.Jingle(Audio.JingleFail);
    }

    private void EndAttemptAsFoul()
    {
        _attempt++;
        _marks[_attempt - 1] = -1;
        _prevMark = 0;
        _ph = Ph.Foul; _phT = 1.2;
    }

    private void BeginThrow()
    {
        // categoric carrier-speed limiter: BCD board anchored anything >= 1300 to 1300
        _carrySpeed = Math.Min(_run.SpeedCms, 1300);
        _ph = Ph.Throwing; _throwT = 0;
    }

    private void Release()
    {
        int deg = (int)Math.Round(_angle);

        // secret: full-tilt vertical launch hits a bird offscreen
        if (deg >= 80 && _carrySpeed >= 1300)
        {
            Game.TriggerEgg();
            double radE = deg * Math.PI / 180.0;
            _jvx = 200; _jvy = 2200;
            _jx = _posCm; _jy = 150;
            _ph = Ph.Egg; _phT = 2.5;
            return;
        }

        // fixed arm thrust: always +330 cm/s on top of the (possibly truncated) carrier speed
        double v = _carrySpeed + 330;
        // hereditary distance bonus: floor(previous meters) / 2, floored, in cm/s
        if (_attempt > 0 && _prevMark > 0) v += Math.Floor(Math.Floor(_prevMark) / 2.0);

        // X-register bug: the angle-bonus subroutine reads garbage here, so NO +60/+30/+10
        // is ever applied. A +2 degree aerodynamic shift moves the real optimum to ~43.
        double rad = (deg + 2) * Math.PI / 180.0;
        _jvx = v * Math.Cos(rad);
        _jvy = v * Math.Sin(rad);
        _jx = _posCm; _jy = 150;
        _ph = Ph.Fly;
        Audio.Tone(700, 90, 0.2);
    }

    private void LandNow()
    {
        _attempt++;
        double meters = (_jx - FoulCm) / 100.0;
        if (meters < 0) meters = 0;

        _rolledOver = meters >= 100.0;
        if (_rolledOver) { meters -= 100.0; Audio.LowBeep(); } // 100.12 m reads as 0.12 m

        meters = Math.Round(meters, 2);
        _marks[_attempt - 1] = meters;
        _prevMark = meters;
        if (meters > _best) _best = meters;
        _ph = Ph.Land; _phT = 2.0;
        Voice.Meters(_marks[_attempt - 1]);
        Audio.Tone(400, 100, 0.2);
    }

    private void FinishEvent()
    {
        Qualified = _best >= _qual;
        Points = Qualified ? (int)((_best - _qual) * 100 + 1000) / 10 * 10 : 0;
        ResultText = _best > 0 ? $"{L.Best} {_best:0.00} M" : L.NoMark;
        _ph = Ph.Done; _phT = 2.2;
        Audio.Jingle(Qualified ? Audio.JingleQualify : Audio.JingleFail);
    }

    public override void Draw()
    {
        double focus = _ph is Ph.Fly ? _jx : _posCm;
        int cam = Math.Max(0, (int)(focus * Scene.PxPerCm) - 70);
        Scene.DrawStadium(cam);
        Scene.DrawField(cam, FoulCm, 110);

        int px = (int)(_posCm * Scene.PxPerCm) - cam;
        switch (_ph)
        {
            case Ph.Wait:
            case Ph.Run:
                if (_run.SpeedCms > 1)
                    Athlete.Run(px, Scene.GroundY, _anim * Math.PI, _run.SpeedCms / 1400, Game.CurJersey);
                else Athlete.Crouch(px, Scene.GroundY, Game.CurJersey);
                DrawJavelinInHand(px);
                break;
            case Ph.Hold:
                Athlete.Throw(px, Scene.GroundY, 0, Game.CurJersey);
                DrawJavelinInHand(px);
                Scene.AngleMeter(_angle);
                break;
            case Ph.Throwing:
                Athlete.Throw(px, Scene.GroundY, _throwT / 0.22, Game.CurJersey);
                Scene.AngleMeter(_angle);
                break;
            case Ph.Fly:
                Athlete.Throw(px, Scene.GroundY, 1, Game.CurJersey);
                int jx = (int)(_jx * Scene.PxPerCm) - cam;
                int jy = Scene.GroundY - (int)(_jy * 0.28);
                double a = Math.Atan2(_jvy, _jvx);
                Gfx.Line(jx, jy, jx + (int)(Math.Cos(a) * 10), jy - (int)(Math.Sin(a) * 10), Gfx.White, 1);
                break;
            case Ph.Egg:
                Athlete.Throw(px, Scene.GroundY, 1, Game.CurJersey);
                // the javelin left the top of the screen and hit an unidentified bird
                int by = (int)((2.5 - _phT) / 2.5 * (Scene.GroundY - 60)) + 40;
                Game.DrawBird(px + 60, by);
                Gfx.Text(px + 56, by + 10, "K.H.", Gfx.White); // author initials
                break;
            case Ph.Land:
                int lx = (int)(_jx * Scene.PxPerCm) - cam;
                Gfx.Line(lx, Scene.GroundY - 8, lx + 4, Scene.GroundY, Gfx.White, 1);
                Gfx.TextCentered(96, $"{_marks[_attempt - 1]:0.00} M", _rolledOver ? Gfx.Red : Gfx.White, 2);
                if (_rolledOver) Gfx.TextCentered(114, L.Rollover, Gfx.Red);
                break;
            case Ph.Foul:
                Gfx.TextCentered(96, L.Foul, Gfx.Red, 2);
                break;
            case Ph.Done:
                Gfx.TextCentered(96, ResultText, Gfx.White, 2);
                break;
        }

        Gfx.Text(8, 4, $"{L.Attempt} {Math.Min(_attempt + (_ph is Ph.Land or Ph.Foul or Ph.Done ? 0 : 1), 3)}/3", Gfx.White);
        Gfx.Text(130, 4, $"{L.Qual} {_qual:0.00}M", Gfx.Cyan);
        Gfx.Text(8, 12, $"{L.Best} {_best:0.00}M", Gfx.Yellow);
        if (_ph is Ph.Run or Ph.Wait or Ph.Hold) Scene.SpeedBar(_run.SpeedCms);
    }

    private static void DrawJavelinInHand(int px)
        => Gfx.Line(px - 4, Scene.GroundY - 22, px + 8, Scene.GroundY - 24, Gfx.White, 1);
}
