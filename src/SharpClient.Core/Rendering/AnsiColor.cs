namespace SharpClient.Core.Rendering;

public enum AnsiColorKind
{
    Default,
    Indexed,
}

public readonly record struct AnsiColor(AnsiColorKind Kind, int Index)
{
    public static AnsiColor Default => new(AnsiColorKind.Default, 0);

    public static AnsiColor Indexed(int index) => new(AnsiColorKind.Indexed, index);
}
