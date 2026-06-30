using SharpClient.Core.Platform;

namespace SharpClient.Core.Presentation;

public sealed class SettingsViewModel
{
    // ── Lookup tables ────────────────────────────────────────────────────────
    public static readonly string[] FontOptions =
    [
        "JetBrains Mono",
        "IBM Plex Mono",
        "Space Mono",
        "Courier",
    ];

    public static readonly string[] AccentOptions =
    [
        "#9b7ed4",
        "#39d98a",
        "#4a90d9",
        "#c08a3e",
        "#56b6c2",
    ];

    // Pre-computed acc2 / soft / line for the 5 known accents
    private static readonly Dictionary<string, (string Acc2, string Soft, string Line)> s_accentMeta =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["#9b7ed4"] = ("#b89fe0", "rgba(155,126,212,.16)", "rgba(155,126,212,.55)"),
            ["#39d98a"] = ("#6de8a8", "rgba(57,217,138,.16)",  "rgba(57,217,138,.55)"),
            ["#4a90d9"] = ("#7ab0e8", "rgba(74,144,217,.16)",  "rgba(74,144,217,.55)"),
            ["#c08a3e"] = ("#d4a969", "rgba(192,138,62,.16)",  "rgba(192,138,62,.55)"),
            ["#56b6c2"] = ("#80cdd6", "rgba(86,182,194,.16)",  "rgba(86,182,194,.55)"),
        };

    private static readonly Dictionary<string, string> s_fontFamilies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["JetBrains Mono"] = "'JetBrains Mono',ui-monospace,monospace",
            ["IBM Plex Mono"]  = "'IBM Plex Mono',ui-monospace,monospace",
            ["Space Mono"]     = "'Space Mono',ui-monospace,monospace",
            ["Courier"]        = "Courier,'Courier New',monospace",
        };

    // ── State ────────────────────────────────────────────────────────────────
    private readonly IPreferences _prefs;
    private string _outputFont;
    private int    _minColumns;
    private int    _maxFontSize;
    private string _accent;
    private bool   _glow;
    private bool   _scanlines;

    public event Action? Changed;

    // ── Constructor ──────────────────────────────────────────────────────────
    public SettingsViewModel(IPreferences prefs)
    {
        _prefs       = prefs;
        _outputFont  = prefs.GetString("OutputFont",  "JetBrains Mono");
        _minColumns  = prefs.GetInt   ("MinColumns",  78);
        _maxFontSize = prefs.GetInt   ("MaxFontSize", 14);
        _accent      = prefs.GetString("Accent",      "#9b7ed4");
        _glow        = prefs.GetBool  ("Glow",        true);
        _scanlines   = prefs.GetBool  ("Scanlines",   true);
    }

    // ── Properties ───────────────────────────────────────────────────────────
    public string OutputFont
    {
        get => _outputFont;
        set { _outputFont = value; _prefs.SetString("OutputFont", value); Changed?.Invoke(); }
    }

    public int MinColumns
    {
        get => _minColumns;
        set { _minColumns = value; _prefs.SetInt("MinColumns", value); Changed?.Invoke(); }
    }

    public int MaxFontSize
    {
        get => _maxFontSize;
        set { _maxFontSize = value; _prefs.SetInt("MaxFontSize", value); Changed?.Invoke(); }
    }

    public string Accent
    {
        get => _accent;
        set { _accent = value; _prefs.SetString("Accent", value); Changed?.Invoke(); }
    }

    public bool Glow
    {
        get => _glow;
        set { _glow = value; _prefs.SetBool("Glow", value); Changed?.Invoke(); }
    }

    public bool Scanlines
    {
        get => _scanlines;
        set { _scanlines = value; _prefs.SetBool("Scanlines", value); Changed?.Invoke(); }
    }

    // ── Computed ─────────────────────────────────────────────────────────────
    public string FontFamily =>
        s_fontFamilies.TryGetValue(_outputFont, out var f) ? f
            : "'JetBrains Mono',ui-monospace,monospace";

    public string RootStyleVariables
    {
        get
        {
            var (acc2, soft, line) = s_accentMeta.TryGetValue(_accent, out var m) ? m
                : ("#b89fe0", "rgba(155,126,212,.16)", "rgba(155,126,212,.55)");
            return $"--acc:{_accent};--acc2:{acc2};--acc-soft:{soft};--acc-line:{line};" +
                   $"--mono:{FontFamily};--out-fs:{_maxFontSize}px;";
        }
    }
}
