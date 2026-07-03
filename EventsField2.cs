namespace TrackAndField;

/// <summary>
/// Hammer Throw. One run tap starts the spin; revolutions ramp the launch speed
/// (max at 9). Release timing inside the rotation sets the elevation angle; the
/// 45 degree +60 cm/s subroutine works correctly here (no X-register bug).
/// </summary>
public class Hammer : EventBase
{
    public override string Name => L.EventNames[4];
    public override string QualText => $"{L.Qualify} {_qual:0.00} M";

    private const int CircleCm = 300;         // front of the throwing circle
    private const double GEff = 981.0 / 3.6;
    private enum Ph { Wait, Spin, Fly, Land, Foul, Done }
    private Ph _ph;
    private double _qual, _phT;
    private double _spinPhase, _totalPhase;   // radians
    private int _revs, _lastTickRev;
    private double _hx, _hy, _hvx, _hvy;
    private int _attempt, _releaseDeg;
    private readonly double[] _marks = new double[3];
    private double _best;

    public override void Reset(int match)
    {
        _qual = Math.Round((match switch { 1 => 75.00, 2 => 80.00, _ => 85.00 }) * Settings.DistF, 2);
        _attempt = 0; _best = 0; Array.Clear(_marks);
        Finished = Qualified = false; Points = 0; ResultText = "";
        NextAttempt();
    }

    private void NextAttempt()
    {
        _ph = Ph.Wait; _phT = 1.0;
        _spinPhase = _totalPhase = 0; _revs = 0; _lastTickRev = -1;
        _releaseDeg = 0;
    }

    public override void Step()
    {
        switch (_ph)
        {
            case Ph.Wait:
                _phT -= Dt;
                if (_phT <= 0 && Input.RunTaps > 0) { _ph = Ph.Spin; Audio.Beep(); }
                break;

            case Ph.Spin:
                double revPerSec = Math.Min(2.6, 0.7 + _revs * 0.22);
                _totalPhase += revPerSec * 2 * Math.PI * Dt;
                _spinPhase = _totalPhase % (2 * Math.PI);
                _revs = (int)(_totalPhase / (2 * Math.PI));
                if (_revs != _lastTickRev) { _lastTickRev = _revs; Audio.Tick(Math.Min(1, _revs / 9.0)); }

                if (_revs > 11) { Foul(L.Dizzy); return; } // held on too long

                if (Input.ActionPressed) Release();
                break;

            case Ph.Fly:
                _hvy -= GEff * Dt;
                _hx += _hvx * Dt;
                _hy += _hvy * Dt;
                if (_hy <= 0) LandNow();
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

    private void Release()
    {
        // facing direction at this exact frame decides the elevation angle
        double deg = _spinPhase * 180.0 / Math.PI;
        if (deg < 5 || deg > 85) { Foul(L.OutOfSector); return; }

        _releaseDeg = (int)Math.Round(deg);
        double v = 920 + Math.Min(_revs, 9) * 70 + AngleBonusCms(_releaseDeg); // bonus intact here
        double rad = _releaseDeg * Math.PI / 180.0;
        _hvx = v * Math.Cos(rad);
        _hvy = v * Math.Sin(rad);
        _hx = CircleCm; _hy = 150;
        _ph = Ph.Fly;
        Audio.Tone(700, 90, 0.25);
    }

    private void Foul(string msg)
    {
        _attempt++;
        _marks[_attempt - 1] = -1;
        _ph = Ph.Foul; _phT = 1.8;
        ResultText = msg;
        Voice.Foul();
        Audio.Jingle(Audio.JingleFail);
    }

    private void LandNow()
    {
        _attempt++;
        double meters = Math.Max(0, Math.Round((_hx - CircleCm) / 100.0, 2));
        _marks[_attempt - 1] = meters;
        if (meters > _best) _best = meters;
        _ph = Ph.Land; _phT = 2.0;
        Voice.Meters(meters);
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
        double focus = _ph == Ph.Fly ? _hx : 0;
        int cam = Math.Max(0, (int)(focus * Scene.PxPerCm) - 80);
        Scene.DrawStadium(cam);
        Scene.DrawField(cam, CircleCm, 110);

        int cx = (int)(CircleCm * Scene.PxPerCm) - cam - 10;
        // throwing circle
        Gfx.FillRect(cx - 12, Scene.GroundY + 2, 26, 4, Gfx.Gray);

        switch (_ph)
        {
            case Ph.Wait:
                Athlete.Spin(cx, Scene.GroundY, Math.PI, Game.CurJersey, out _, out _);
                Gfx.TextCentered(96, L.PressRunToSpin, Gfx.Yellow);
                break;
            case Ph.Spin:
                Athlete.Spin(cx, Scene.GroundY, _spinPhase, Game.CurJersey, out _, out _);
                Gfx.Text(8, 12, $"{L.Revs} {_revs}", _revs >= 9 ? Gfx.Red : Gfx.Yellow);
                // release-direction hint dial
                double d = _spinPhase;
                Gfx.Circle(226, 206, 2, Gfx.White);
                Gfx.Line(226, 206, 226 + (int)(Math.Cos(d) * 12), 206 - (int)(Math.Sin(d) * 12), Gfx.Yellow, 1);
                break;
            case Ph.Fly:
                Athlete.Throw(cx, Scene.GroundY, 1, Game.CurJersey);
                int hx = (int)(_hx * Scene.PxPerCm) - cam;
                int hy = Scene.GroundY - (int)(_hy * 0.28);
                Gfx.Circle(hx, hy, 2, Gfx.DarkGray);
                Gfx.Text(8, 12, $"ANGLE {_releaseDeg}~", Gfx.Yellow);
                break;
            case Ph.Land:
                Gfx.TextCentered(96, $"{_marks[_attempt - 1]:0.00} M", Gfx.White, 2);
                Gfx.TextCentered(114, $"ANGLE {_releaseDeg}~", Gfx.Yellow);
                break;
            case Ph.Foul:
                Gfx.TextCentered(96, ResultText, Gfx.Red);
                break;
            case Ph.Done:
                Gfx.TextCentered(96, ResultText, Gfx.White, 2);
                break;
        }

        Gfx.Text(8, 4, $"{L.Attempt} {Math.Min(_attempt + (_ph is Ph.Land or Ph.Foul or Ph.Done ? 0 : 1), 3)}/3", Gfx.White);
        Gfx.Text(130, 4, $"{L.Qual} {_qual:0.00}M", Gfx.Cyan);
    }
}

/// <summary>
/// High Jump. Automatic approach; hold ACTION at the threshold to flatten the arc
/// (light press = pure 90 degrees = crash). Tapping RUN mid-air claws altitude back.
/// Bar sprite corrupts above 2.47 m and crumbles above 2.56 m (video memory bug).
/// </summary>
public class HighJump : EventBase
{
    public override string Name => L.EventNames[5];
    public override string QualText => $"{L.Qualify} {_qual:0.00} M";

    private const double BarXCm = 700;        // bar plane
    private const double TakeoffCm = 660;     // hold zone begins
    private const double ApproachSpeed = 550; // cm/s auto-run
    private const double LaunchV = 880;       // cm/s
    private const double G = 1400;            // arcade gravity
    private const double ClawGain = 12;       // cm/s of vy per mid-air tap

    private enum Ph { Wait, Approach, Hold, Fly, Cleared, Foul, Done }
    private Ph _ph;
    private double _qual, _phT, _posCm, _anim, _angle;
    private double _jx, _jy, _jvx, _jvy;
    private bool _crossed, _hitBar;
    private double _barM;
    private int _fouls, _foulsAtHeight;
    private double _best;
    private double _barCrumbleT;

    public override void Reset(int match)
    {
        _qual = Math.Round((match switch { 1 => 2.28, 2 => 2.35, _ => 2.40 }) * Settings.DistF, 2);
        _barM = _qual;
        _fouls = 0; _foulsAtHeight = 0; _best = 0;
        Finished = Qualified = false; Points = 0; ResultText = "";
        NextAttempt();
    }

    private void NextAttempt()
    {
        _ph = Ph.Wait; _phT = 1.0;
        _posCm = 0; _anim = 0; _angle = 90;
        _crossed = _hitBar = false;
        _barCrumbleT = 0;
    }

    public override void Step()
    {
        switch (_ph)
        {
            case Ph.Wait:
                _phT -= Dt;
                if (_phT <= 0) { _ph = Ph.Approach; Audio.Beep(); }
                break;

            case Ph.Approach:
                _posCm += ApproachSpeed * Dt;
                _anim += ApproachSpeed * Dt * 0.02;
                if (Input.ActionPressed && _posCm >= TakeoffCm - 150) { _ph = Ph.Hold; _angle = 90; }
                else if (_posCm >= BarXCm - 20) Foul(); // ran into the bar
                break;

            case Ph.Hold:
                _posCm += ApproachSpeed * 0.35 * Dt;
                _angle = Math.Max(40, _angle - 1.0); // longer hold = flatter arc
                if (_posCm >= BarXCm - 15) { Launch(); return; }
                if (Input.ActionReleased) Launch();
                break;

            case Ph.Fly:
                // the mid-air crawl anomaly: run taps partially cancel downward acceleration
                _jvy += Input.RunTaps * ClawGain;
                _jvy -= G * Dt;
                _jx += _jvx * Dt;
                _jy += _jvy * Dt;

                double barCm = _barM * 100;
                if (!_crossed && _jx >= BarXCm)
                {
                    _crossed = true;
                    if (_jy <= barCm + 2) _hitBar = true;
                }
                // falling back onto the bar without crossing
                if (!_crossed && _jvy < 0 && _jy <= barCm && _jx > BarXCm - 25) _hitBar = true;

                if (_jy <= (_jx > BarXCm - 10 ? 40 : 0)) // mat top = 40 cm
                {
                    if (_crossed && !_hitBar) Clear();
                    else Foul();
                }
                break;

            case Ph.Cleared:
                _phT -= Dt;
                if (_barM > 2.56) _barCrumbleT += Dt;
                if (_phT <= 0)
                {
                    _barM = Math.Round(_barM + 0.03, 2); // bar rises, keep jumping
                    _foulsAtHeight = 0;
                    NextAttempt();
                }
                break;

            case Ph.Foul:
                _phT -= Dt;
                if (_phT <= 0)
                {
                    if (_fouls >= 3) FinishEvent();
                    else NextAttempt();
                }
                break;

            case Ph.Done:
                _phT -= Dt;
                if (_phT <= 0) Finished = true;
                break;
        }
    }

    private void Launch()
    {
        double rad = _angle * Math.PI / 180.0;
        _jvx = LaunchV * Math.Cos(rad);
        _jvy = LaunchV * Math.Sin(rad);
        _jx = _posCm; _jy = 1;
        _ph = Ph.Fly;
        Audio.Tone(600, 80, 0.2);
    }

    private void Clear()
    {
        _best = _barM;
        // secret: two fouls at this height, then a clean clear on the final attempt
        if (_foulsAtHeight >= 2) Game.TriggerEgg();
        _ph = Ph.Cleared; _phT = 2.0;
        Voice.Meters(_barM);
        Audio.Jingle(Audio.JingleQualify);
    }

    private void Foul()
    {
        _fouls++; _foulsAtHeight++;
        _ph = Ph.Foul; _phT = 1.6;
        Voice.Foul();
        Audio.Jingle(Audio.JingleFail);
    }

    private void FinishEvent()
    {
        Qualified = _best >= _qual;
        Points = Qualified ? (int)((_best - _qual) * 4000 + 1000) / 10 * 10 : 0;
        ResultText = _best > 0 ? $"{L.Best} {_best:0.00} M" : L.NoMark;
        _ph = Ph.Done; _phT = 2.2;
        Audio.Jingle(Qualified ? Audio.JingleQualify : Audio.JingleFail);
    }

    public override void Draw()
    {
        Scene.DrawStadium(0);
        Gfx.FillRect(0, 88, Gfx.W, Gfx.H - 88, Gfx.Grass);
        for (int y = 88; y < Gfx.H; y += 8) Gfx.HLine(0, y, Gfx.W, Gfx.GrassDark);
        Gfx.FillRect(0, Scene.GroundY - 3, (int)(BarXCm * Scene.PxPerCm) - 6, 10, Gfx.TrackRed);

        int barX = (int)(BarXCm * Scene.PxPerCm);
        int barY = Scene.GroundY - (int)(_barM * 100 * 0.35);
        // mat
        Gfx.FillRect(barX - 2, Scene.GroundY - 14, 46, 18, Gfx.Blue);
        // standards
        Gfx.VLine(barX - 3, barY - 6, Scene.GroundY - barY + 6, Gfx.Gray);
        Gfx.VLine(barX + 40, barY - 6, Scene.GroundY - barY + 6, Gfx.Gray);
        DrawBar(barX, barY);

        switch (_ph)
        {
            case Ph.Wait:
            case Ph.Approach:
                int px = (int)(_posCm * Scene.PxPerCm);
                Athlete.Run(px, Scene.GroundY, _anim * Math.PI, 0.6, Game.CurJersey);
                break;
            case Ph.Hold:
                int hx = (int)(_posCm * Scene.PxPerCm);
                Athlete.Crouch(hx, Scene.GroundY, Game.CurJersey);
                Scene.AngleMeter(_angle);
                break;
            case Ph.Fly:
                int fx = (int)(_jx * Scene.PxPerCm);
                int fy = Scene.GroundY - (int)(_jy * 0.35);
                Athlete.Fly(fx, fy, Game.CurJersey, _crossed ? 70 : 20, -160);
                Gfx.Text(8, 12, $"H {_jy / 100:0.00}M", Gfx.Yellow);
                break;
            case Ph.Cleared:
                Athlete.Celebrate(barX + 20, Scene.GroundY, _phT, Game.CurJersey);
                Gfx.TextCentered(96, $"{_barM:0.00} M {L.Cleared}", Gfx.White, 2);
                break;
            case Ph.Foul:
                Athlete.Fallen(barX + (_crossed ? 20 : -14), Scene.GroundY, Game.CurJersey);
                Gfx.TextCentered(96, _hitBar ? L.BarDown : L.Foul, Gfx.Red, 2);
                break;
            case Ph.Done:
                Gfx.TextCentered(96, ResultText, Gfx.White, 2);
                break;
        }

        Gfx.Text(8, 4, $"{L.Bar} {_barM:0.00}M  {L.Miss} {_fouls}/3", Gfx.White);
        Gfx.Text(160, 4, $"{L.Qual} {_qual:0.00}M", Gfx.Cyan);
    }

    private void DrawBar(int barX, int barY)
    {
        if (_hitBar && _ph == Ph.Foul)
        {
            Gfx.Line(barX, Scene.GroundY - 2, barX + 38, Scene.GroundY - 6, Gfx.Yellow, 1);
            return;
        }
        if (_barM > 2.56)
        {
            // VRAM mapping corruption: the bar crumbles and slides down
            int slide = (int)(_barCrumbleT * 20);
            for (int i = 0; i < 38; i += 4)
                Gfx.Px(barX + i, barY + (i % 8) / 2 + slide, Gfx.Yellow);
        }
        else if (_barM > 2.47)
        {
            // gaps appear in the bar sprite
            for (int i = 0; i < 38; i++)
                if (i % 3 != 2) Gfx.Px(barX + i, barY, Gfx.Yellow);
        }
        else
        {
            Gfx.HLine(barX, barY, 38, Gfx.Yellow);
        }
    }
}
