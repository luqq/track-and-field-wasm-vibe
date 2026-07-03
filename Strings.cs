namespace TrackAndField;

/// <summary>
/// Localization tables. Index 0 = castellano, 1 = english, 2 = català.
/// Accents are omitted (5x7 arcade font), but Ñ and Ç glyphs exist.
/// </summary>
public static class L
{
    private static int I => Settings.Lang;

    public static readonly string[] LangNames = { "CASTELLANO", "ENGLISH", "CATALA" };
    public static string SpeechTag => I switch { 0 => "es-ES", 1 => "en-US", _ => "ca-ES" };

    // --- menus ---
    public static string OnePlayer => Pick("1 JUGADOR", "1 PLAYER", "1 JUGADOR");
    public static string TwoPlayers => Pick("2 JUGADORES", "2 PLAYERS", "2 JUGADORS");
    public static string Options => Pick("OPCIONES", "OPTIONS", "OPCIONS");
    public static string Difficulty => Pick("DIFICULTAD", "DIFFICULTY", "DIFICULTAT");
    public static string[] DiffNames => I switch
    {
        0 => new[] { "FACIL", "NORMAL", "DIFICIL" },
        1 => new[] { "EASY", "NORMAL", "HARD" },
        _ => new[] { "FACIL", "NORMAL", "DIFICIL" },
    };
    public static string Language => Pick("IDIOMA", "LANGUAGE", "IDIOMA");
    public static string VoiceLbl => Pick("VOZ", "VOICE", "VEU");
    public static string On => Pick("SI", "ON", "SI");
    public static string Off => Pick("NO", "OFF", "NO");
    public static string Keys => Pick("REDEFINIR TECLAS", "REDEFINE KEYS", "REDEFINIR TECLES");
    public static string KeysDefault => Pick("TECLAS POR DEFECTO", "DEFAULT KEYS", "TECLES PER DEFECTE");
    public static string Back => Pick("VOLVER", "BACK", "TORNAR");
    public static string PressKeyFor => Pick("PULSA TECLA O BOTON PARA", "PRESS KEY OR BUTTON FOR", "PREM TECLA O BOTO PER A");
    public static string[] ActionNames => I switch
    {
        0 => new[] { "CARRERA A", "CARRERA B", "ACCION", "START" },
        1 => new[] { "RUN A", "RUN B", "ACTION", "START" },
        _ => new[] { "CURSA A", "CURSA B", "ACCIO", "START" },
    };
    public static string MenuHint => Pick("CARRERA = MOVER  ACCION = OK", "RUN = MOVE  ACTION = OK", "CURSA = MOURE  ACCIO = OK");

    // --- game flow ---
    public static string Player => Pick("JUGADOR", "PLAYER", "JUGADOR");
    public static string Qualify => Pick("CLASIFICA", "QUALIFY", "CLASSIFICA");
    public static string Sec => Pick("SEG", "SEC", "SEG");
    public static string Time => Pick("TIEMPO", "TIME", "TEMPS");
    public static string Qual => Pick("MIN", "QUAL", "MIN");
    public static string Attempt => Pick("INTENTO", "ATTEMPT", "INTENT");
    public static string Best => Pick("MEJOR", "BEST", "MILLOR");
    public static string Foul => Pick("NULO!", "FOUL!", "NUL!");
    public static string Qualified => Pick("CLASIFICADO!", "QUALIFIED!", "CLASSIFICAT!");
    public static string NotQualified => Pick("NO CLASIFICADO", "NOT QUALIFIED", "NO CLASSIFICAT");
    public static string ExtraLife => Pick("VIDA EXTRA - REINTENTA", "EXTRA LIFE - TRY AGAIN", "VIDA EXTRA - TORNA-HI");
    public static string GameOver => Pick("FIN DE PARTIDA", "GAME OVER", "FI DE PARTIDA");
    public static string Rival => "RIVAL";
    public static string OnYourMarks => Pick("A SUS PUESTOS", "ON YOUR MARKS", "ALS SEUS LLOCS");
    public static string Set => Pick("LISTOS...", "SET...", "PREPARATS...");
    public static string FalseStart => Pick("SALIDA FALSA", "FALSE START", "SORTIDA FALSA");
    public static string Disqualified => Pick("DESCALIFICADO!", "DISQUALIFIED!", "DESQUALIFICAT!");
    public static string NoMark => Pick("SIN MARCA", "NO MARK", "SENSE MARCA");
    public static string Rollover => Pick("DESBORDAMIENTO!", "COUNTER ROLLOVER!", "DESBORDAMENT!");
    public static string Dizzy => Pick("MAREADO! NULO", "DIZZY! FOUL", "MAREJAT! NUL");
    public static string OutOfSector => Pick("FUERA DE SECTOR! NULO", "OUT OF SECTOR! FOUL", "FORA DE SECTOR! NUL");
    public static string Cleared => Pick("SUPERADO!", "CLEARED!", "SUPERAT!");
    public static string BarDown => Pick("LISTON DERRIBADO! NULO", "BAR DOWN! FOUL", "LLISTO A TERRA! NUL");
    public static string PressRunToSpin => Pick("PULSA CARRERA PARA GIRAR", "PRESS RUN TO SPIN", "PREM CURSA PER GIRAR");
    public static string Secret => Pick("SECRETO! +1000 PTS", "SECRET! +1000 PTS", "SECRET! +1000 PTS");
    public static string Bonus => "BONUS";
    public static string Revs => Pick("GIROS", "REVS", "GIRS");
    public static string Speed => Pick("VEL", "SPEED", "VEL");
    public static string Bar => Pick("LISTON", "BAR", "LLISTO");
    public static string Miss => Pick("NULOS", "MISS", "NULS");
    public static string PushStart => Pick("PULSA ACCION PARA EMPEZAR", "PUSH ACTION TO START", "PREM ACCIO PER COMENCAR");

    public static string[] EventNames => I switch
    {
        0 => new[] { "100M LISOS", "SALTO DE LONGITUD", "LANZAMIENTO DE JABALINA", "110M VALLAS", "LANZAMIENTO DE MARTILLO", "SALTO DE ALTURA" },
        1 => new[] { "100M DASH", "LONG JUMP", "JAVELIN THROW", "110M HURDLES", "HAMMER THROW", "HIGH JUMP" },
        _ => new[] { "100M LLISOS", "SALT DE LLARGADA", "LLANÇAMENT DE JAVELINA", "110M TANQUES", "LLANÇAMENT DE MARTELL", "SALT D'ALÇADA" },
    };

    public static string[] EventHints => I switch
    {
        0 => new[] { "MACHACA CARRERA TRAS EL DISPARO!", "CORRE - MANTEN ACCION - APUNTA 45~", "CORRE - MANTEN ACCION - APUNTA 43~", "MACHACA CARRERA - ACCION SALTA", "UNA PULSACION - ACCION EN 45~", "MANTEN ACCION EN EL LISTON" },
        1 => new[] { "MASH RUN AFTER THE GUN!", "RUN - HOLD ACTION - AIM 45~", "RUN - HOLD ACTION - AIM 43~", "MASH RUN - ACTION TO JUMP", "TAP RUN ONCE - ACTION AT 45~", "HOLD ACTION AT THE BAR" },
        _ => new[] { "PICA CURSA DESPRES DEL TRET!", "CORRE - MANTEN ACCIO - APUNTA 45~", "CORRE - MANTEN ACCIO - APUNTA 43~", "PICA CURSA - ACCIO PER SALTAR", "UNA PULSACIO - ACCIO A 45~", "MANTEN ACCIO AL LLISTO" },
    };

    private static string Pick(string es, string en, string ca) => I switch { 0 => es, 1 => en, _ => ca };

    // --- speech (local TTS) ---
    private static string Num(double v) => I == 1 ? $"{v:0.00}" : $"{v:0.00}".Replace('.', ',');
    public static string SpeakMeters(double m) => $"{Num(m)} " + Pick("metros", "meters", "metres");
    public static string SpeakTime(double s) => $"{Num(s)} " + Pick("segundos", "seconds", "segons");
    public static string SpeakFoul => Pick("Nulo", "Foul", "Nul");
    public static string SpeakQualified => Pick("Clasificado", "Qualified", "Classificat");
    public static string SpeakNotQualified => Pick("No clasificado", "Not qualified", "No classificat");
}

/// <summary>Local speech synthesis announcer (Web Speech API), gated by the settings toggle.</summary>
public static class Voice
{
    private static void Say(string text)
    {
        if (Settings.VoiceOn) Audio.Say(text, L.SpeechTag);
    }

    public static void Meters(double m) => Say(L.SpeakMeters(m));
    public static void Time(double s) => Say(L.SpeakTime(s));
    public static void Foul() => Say(L.SpeakFoul);
    public static void Qualified(bool ok) => Say(ok ? L.SpeakQualified : L.SpeakNotQualified);
}
