namespace SharpClient.Core.Rendering;

public readonly record struct TextStyle
{
    public AnsiColor Foreground { get; init; }

    public AnsiColor Background { get; init; }

    public bool Bold { get; init; }

    public bool Underline { get; init; }

    public bool Inverse { get; init; }

    public static TextStyle Default => new()
    {
        Foreground = AnsiColor.Default,
        Background = AnsiColor.Default,
    };
}
