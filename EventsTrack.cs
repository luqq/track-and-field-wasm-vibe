namespace TrackAndField;

public abstract class EventBase
{
    public bool Finished;
    public bool Qualified;
    public int Points;
    public string ResultText = "";
    public abstract string Name { get; }
    public abstract string QualText { get; }
    public abstract void Reset(int match);
    public abstract void Step();
    public abstract void Draw();

    protected const double Dt = 1.0 / 60.0;

    protected static int AngleBonusCms(int deg) => deg switch
    {
        45 => 60,
        44 or 46 => 30,
        43 or 47 => 10,
        _ => 0
    };
}

/// <summary>100m Dash. Pure button mashing, false starts, crouch-to-run latency, CPU rival.</summary>
public class Dash100 : EventBase
{
    public override string Name => L.EventNames[0];
    public override string QualText => $"{L.Qualify} {_qual:0.00} {L.Sec}";

    private enum Ph { Marks, Set, Run, Done }
    private Ph _ph;
    private double _phT, _qual;
    private readonly RunMeter _run = new();
    private double _distCm, _timer, _anim, _standT;
    private int _falseStarts;
    private bool _dq, _pDone;
    private double _pTime;
    private double _rDist, _rSpeed, _rCruise, _rTime;
    private bool _rDone;
    private double _doneT;
    private const int TotalCm = 10000;

    public override void Reset(int match)
    {
        _qual = Math.Round((match switch { 1 => 13.50, 2 => 10.50, _ => 10.00 }) * Settings.TimeF, 2);
        _ph = Ph.Marks; _phT = 1.4;
        _run.Reset();
        _run.TapGain = 55 * Settings.TapF;
        _distCm = _timer = _anim = _standT = 0;
        _falseStarts = 0; _dq = _pDone = _rDone = false;
        _pTime = _rTime = 0; _rDist = _rSpeed = 0; _doneT = 0;
        Finished = Qualified = false; Points = 0; ResultText = "";
        // rival paces just inside the qualifying mark
        double target = _qual * 0.99;
        _rCruise = TotalCm / (target - 0.35); // 0.35 s of ramp-up loss
    }

    public override void Step()
    {
        _phT -= Dt;
        switch (_ph)
        {
            case Ph.Marks:
                if (_phT <= 0) { _ph = Ph.Set; _phT = 1.0 + Game.Rng.NextDouble(); Audio.Beep(); }
                break;

            case Ph.Set:
                // premature run pulse = false start... unless within the free 0.12 s pre-gun window
                if (Input.RunTaps > 0)
                {
                    if (_phT > 0.12)
                    {
                        _falseStarts++;
                        Audio.LowBeep();
                        if (_falseStarts >= 3) { _dq = true; Finish(); return; }
                        _ph = Ph.Marks; _phT = 1.4;
                        return;
                    }
                    _run.Step(Input.RunTaps); // legal pre-charge, no movement yet
                }
                if (_phT <= 0)
                {
                    _ph = Ph.Run;
                    Audio.Jingle(Audio.JingleGun);
                }
                break;

            case Ph.Run:
                _timer += Dt;
                if (!_pDone)
                {
                    if (Input.RunTaps > 0 && _standT == 0 && _distCm == 0) _standT = 0.30;
                    _run.Step(Input.RunTaps);
                    double eff = _run.SpeedCms;
                    if (_standT > 0) { _standT -= Dt; eff *= 0.4; } // rising from the crouch wastes time
                    _distCm += eff * Dt;
                    _anim += eff * Dt * 0.02;
                    if (_distCm >= TotalCm)
                    {
                        _pDone = true;
                        _pTime = _timer - (_distCm - TotalCm) / Math.Max(1, eff);
                    }
                }
                if (!_rDone)
                {
                    _rSpeed = Math.Min(_rCruise, _rSpeed + _rCruise * Dt / 0.7);
                    _rDist += _rSpeed * Dt;
                    if (_rDist >= TotalCm)
                    {
                        _rDone = true;
                        _rTime = _timer - (_rDist - TotalCm) / _rSpeed;
                    }
                }
                if (_pDone && (_rDone || _timer > 25) || _timer > 30) Finish();
                break;

            case Ph.Done:
                _doneT += Dt;
                if (_doneT > 2.5) Finished = true;
                break;
        }
    }

    private void Finish()
    {
        _ph = Ph.Done; _doneT = 0;
        if (_dq)
        {
            Qualified = false; Points = 0; ResultText = L.Disqualified;
            Audio.Jingle(Audio.JingleFail);
            return;
        }
        if (!_pDone) _pTime = 99.99;
        Qualified = _pTime <= _qual;
        Points = Qualified ? (int)((_qual - _pTime) * 2000 + 1000) / 10 * 10 : 0;
        ResultText = $"{L.Time} {_pTime:0.00}";
        Voice.Time(_pTime);
        // perfect tie with the rival: Tutankham explorer easter egg
        if (_rDone && Math.Abs(Math.Round(_pTime, 2) - Math.Round(_rTime, 2)) < 0.005)
            Game.TriggerEgg();
    }

    public override void Draw()
    {
        int cam = Math.Max(0, (int)(_distCm * Scene.PxPerCm) - 60);
        Scene.DrawStadium(cam);
        Scene.DrawTrack(cam, TotalCm);

        int px = (int)(_distCm * Scene.PxPerCm) - cam;
        int rx = (int)(_rDist * Scene.PxPerCm) - cam;

        if (_ph is Ph.Marks or Ph.Set)
        {
            Athlete.Crouch(px, Scene.GroundY, Game.CurJersey);
            Athlete.Crouch(rx, Scene.RivalY, Gfx.Blue);
            Gfx.TextCentered(96, _ph == Ph.Marks ? L.OnYourMarks : L.Set, Gfx.Yellow);
        }
        else
        {
            if (_distCm > 0 || _run.SpeedCms > 0)
                Athlete.Run(px, Scene.GroundY, _anim * Math.PI, _run.SpeedCms / 1400, Game.CurJersey);
            else Athlete.Crouch(px, Scene.GroundY, Game.CurJersey);
            Athlete.Run(rx, Scene.RivalY, _timer * 12, _rSpeed / 1400, Gfx.Blue);
        }

        Gfx.Text(8, 4, $"{L.Time} {_timer:00.00}", Gfx.White);
        Gfx.Text(150, 4, $"{L.Qual} {_qual:0.00}", Gfx.Cyan);
        if (_falseStarts > 0) Gfx.Text(8, 12, $"{L.FalseStart} {_falseStarts}", Gfx.Red);
        Scene.SpeedBar(_run.SpeedCms);

        if (_ph == Ph.Done)
        {
            Gfx.TextCentered(96, ResultText, _dq ? Gfx.Red : Gfx.White, 2);
            if (_rDone && !_dq) Gfx.TextCentered(114, $"{L.Rival} {_rTime:0.00}", Gfx.Cyan);
        }
    }
}

/// <summary>110m Hurdles. One single attempt; hitting a hurdle almost kills all momentum.</summary>
public class Hurdles110 : EventBase
{
    public override string Name => L.EventNames[3];
    public override string QualText => $"{L.Qualify} {_qual:0.00} {L.Sec}";

    private enum Ph { Marks, Set, Run, Done }
    private Ph _ph;
    private double _phT, _qual;
    private readonly RunMeter _run = new();
    private double _distCm, _timer, _anim;
    private double _jumpT;              // >0 while airborne
    private const double JumpDur = 0.46;
    private bool _pDone; private double _pTime;
    private double _rDist, _rSpeed, _rCruise, _rTime, _rJumpT;
    private bool _rDone;
    private double _doneT, _stumbleT;
    private readonly bool[] _knocked = new bool[10];
    private readonly bool[] _rKnocked = new bool[10];
    private const int TotalCm = 11000;

    private static double HurdleCm(int i) => 1372 + i * 914;

    public override void Reset(int match)
    {
        _qual = Math.Round((match switch { 1 => 14.00, 2 => 13.00, _ => 12.50 }) * Settings.TimeF, 2);
        _ph = Ph.Marks; _phT = 1.4;
        _run.Reset();
        _run.TapGain = 55 * Settings.TapF;
        _distCm = _timer = _anim = _jumpT = _rJumpT = _stumbleT = 0;
        _pDone = _rDone = false; _pTime = _rTime = 0;
        _rDist = _rSpeed = 0; _doneT = 0;
        Array.Clear(_knocked); Array.Clear(_rKnocked);
        Finished = Qualified = false; Points = 0; ResultText = "";
        _rCruise = TotalCm / (_qual * 0.99 - 0.35);
    }

    private double JumpHeightCm(double t) // simple parabola, apex ~55 cm over the ground
    {
        double n = t / JumpDur;
        return 220 * n * (1 - n); // apex 55 cm * 4 factor => 55 at n=0.5
    }

    public override void Step()
    {
        _phT -= Dt;
        switch (_ph)
        {
            case Ph.Marks:
                if (_phT <= 0) { _ph = Ph.Set; _phT = 1.0 + Game.Rng.NextDouble(); Audio.Beep(); }
                break;
            case Ph.Set:
                if (Input.RunTaps > 0 && _phT > 0.12) { _ph = Ph.Marks; _phT = 1.4; Audio.LowBeep(); return; }
                if (Input.RunTaps > 0) _run.Step(Input.RunTaps);
                if (_phT <= 0) { _ph = Ph.Run; Audio.Jingle(Audio.JingleGun); }
                break;

            case Ph.Run:
                _timer += Dt;
                if (!_pDone)
                {
                    _run.Step(_stumbleT > 0 ? 0 : Input.RunTaps);
                    if (_stumbleT > 0) _stumbleT -= Dt;

                    if (Input.ActionPressed && _jumpT == 0 && _stumbleT <= 0) { _jumpT = Dt; Audio.Tick(0.5); }
                    else if (_jumpT > 0) { _jumpT += Dt; if (_jumpT >= JumpDur) _jumpT = 0; }

                    double prev = _distCm;
                    _distCm += _run.SpeedCms * Dt;
                    _anim += _run.SpeedCms * Dt * 0.02;

                    for (int i = 0; i < 10; i++)
                    {
                        double h = HurdleCm(i);
                        if (prev < h && _distCm >= h && !_knocked[i])
                        {
                            double alt = _jumpT > 0 ? JumpHeightCm(_jumpT) : 0;
                            if (alt < 45)
                            {
                                // crash: momentum nearly obliterated, rebuild from a comatose state
                                _knocked[i] = true;
                                _run.SpeedCms *= 0.10;
                                _stumbleT = 0.7; _jumpT = 0;
                                Audio.LowBeep();
                            }
                        }
                    }
                    if (_distCm >= TotalCm) { _pDone = true; _pTime = _timer; }
                }
                if (!_rDone)
                {
                    _rSpeed = Math.Min(_rCruise, _rSpeed + _rCruise * Dt / 0.7);
                    double prevR = _rDist;
                    _rDist += _rSpeed * Dt;
                    for (int i = 0; i < 10; i++) // rival auto-jumps
                        if (prevR < HurdleCm(i) - 120 && _rDist >= HurdleCm(i) - 120) _rJumpT = Dt;
                    if (_rJumpT > 0) { _rJumpT += Dt; if (_rJumpT >= JumpDur) _rJumpT = 0; }
                    if (_rDist >= TotalCm) { _rDone = true; _rTime = _timer; }
                }
                if (_pDone && (_rDone || _timer > 30) || _timer > 40) Finish();
                break;

            case Ph.Done:
                _doneT += Dt;
                if (_doneT > 2.5) Finished = true;
                break;
        }
    }

    private void Finish()
    {
        _ph = Ph.Done; _doneT = 0;
        if (!_pDone) _pTime = 99.99;
        Qualified = _pTime <= _qual;
        Points = Qualified ? (int)((_qual - _pTime) * 2000 + 1000) / 10 * 10 : 0;
        ResultText = $"{L.Time} {_pTime:0.00}";
        Voice.Time(_pTime);
        if (_rDone && Math.Abs(Math.Round(_pTime, 2) - Math.Round(_rTime, 2)) < 0.005)
            Game.TriggerEgg();
    }

    public override void Draw()
    {
        int cam = Math.Max(0, (int)(_distCm * Scene.PxPerCm) - 60);
        Scene.DrawStadium(cam);
        Scene.DrawTrack(cam, TotalCm);

        for (int i = 0; i < 10; i++)
        {
            int hx = (int)(HurdleCm(i) * Scene.PxPerCm) - cam;
            if (hx > -16 && hx < Gfx.W + 16)
            {
                Props.DrawHurdle(hx, Scene.RivalY, _rKnocked[i]);
                Props.DrawHurdle(hx, Scene.GroundY, _knocked[i]);
            }
        }

        int px = (int)(_distCm * Scene.PxPerCm) - cam;
        int rx = (int)(_rDist * Scene.PxPerCm) - cam;
        int pyOff = _jumpT > 0 ? (int)(JumpHeightCm(_jumpT) * 0.2) : 0;
        int ryOff = _rJumpT > 0 ? (int)(JumpHeightCm(_rJumpT) * 0.2) : 0;

        if (_ph is Ph.Marks or Ph.Set)
        {
            Athlete.Crouch(px, Scene.GroundY, Game.CurJersey);
            Athlete.Crouch(rx, Scene.RivalY, Gfx.Blue);
            Gfx.TextCentered(96, _ph == Ph.Marks ? L.OnYourMarks : L.Set, Gfx.Yellow);
        }
        else
        {
            if (_stumbleT > 0) Athlete.Fallen(px, Scene.GroundY, Game.CurJersey);
            else if (_jumpT > 0) Athlete.Fly(px, Scene.GroundY - pyOff, Game.CurJersey, 60);
            else Athlete.Run(px, Scene.GroundY, _anim * Math.PI, _run.SpeedCms / 1400, Game.CurJersey);

            if (_rJumpT > 0) Athlete.Fly(rx, Scene.RivalY - ryOff, Gfx.Blue, 60);
            else Athlete.Run(rx, Scene.RivalY, _timer * 12, _rSpeed / 1400, Gfx.Blue);
        }

        Gfx.Text(8, 4, $"{L.Time} {_timer:00.00}", Gfx.White);
        Gfx.Text(150, 4, $"{L.Qual} {_qual:0.00}", Gfx.Cyan);
        Scene.SpeedBar(_run.SpeedCms);

        if (_ph == Ph.Done)
        {
            Gfx.TextCentered(96, ResultText, Gfx.White, 2);
            if (_rDone) Gfx.TextCentered(114, $"{L.Rival} {_rTime:0.00}", Gfx.Cyan);
        }
    }
}
