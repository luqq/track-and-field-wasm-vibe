namespace TrackAndField;

/// <summary>
/// Button state fed from JS key/gamepad events. Faithful to the 1983 board:
/// the two RUN buttons are NOT discriminated — any rising edge on either counts.
/// </summary>
public static class Input
{
    public const int RunA = 0, RunB = 1, Action = 2, Start = 3;

    private static readonly bool[] Down = new bool[4];
    private static int _pendingRunTaps;
    private static bool _pendingActionPress;
    private static bool _pendingActionRelease;
    private static bool _pendingStartPress;

    // Consumed each fixed step:
    public static int RunTaps { get; private set; }
    public static bool ActionPressed { get; private set; }
    public static bool ActionReleased { get; private set; }
    public static bool ActionDown => Down[Action];
    public static bool StartPressed { get; private set; }

    /// <summary>Called from JS on keydown/keyup/gamepad transitions.</summary>
    public static void OnButton(int button, bool isDown)
    {
        if ((uint)button >= 4) return;
        bool was = Down[button];
        Down[button] = isDown;
        if (isDown && !was)
        {
            if (button is RunA or RunB) _pendingRunTaps++;
            else if (button == Action) _pendingActionPress = true;
            else if (button == Start) _pendingStartPress = true;
        }
        if (!isDown && was && button == Action) _pendingActionRelease = true;
    }

    /// <summary>Latch pending events for one simulation step.</summary>
    public static void BeginStep()
    {
        RunTaps = _pendingRunTaps; _pendingRunTaps = 0;
        ActionPressed = _pendingActionPress; _pendingActionPress = false;
        ActionReleased = _pendingActionRelease; _pendingActionRelease = false;
        StartPressed = _pendingStartPress; _pendingStartPress = false;
    }
}

/// <summary>
/// Shared locomotion model. Speed is an "atomic" value in cm/s (the original board
/// tracked it in BCD centimeters per second). Rising edges add speed; friction decays it.
/// </summary>
public class RunMeter
{
    public double SpeedCms;               // current speed, cm/s
    public const double TapGain = 55.0;   // cm/s added per valid button edge
    public const double Friction = 0.008; // proportional decay per frame (60 Hz)
    public const double MaxSpeed = 1700.0;

    public void Step(int taps)
    {
        SpeedCms += taps * TapGain;
        SpeedCms -= SpeedCms * Friction;
        if (SpeedCms > MaxSpeed) SpeedCms = MaxSpeed;
        if (SpeedCms < 0.5) SpeedCms = 0;
    }

    public void Reset() => SpeedCms = 0;
}
