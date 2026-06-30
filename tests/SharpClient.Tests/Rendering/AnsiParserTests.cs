using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class AnsiParserTests
{
    [Test]
    public async Task DefaultStyleHasDefaultColours()
    {
        var style = TextStyle.Default;

        await Assert.That(style.Foreground).IsEqualTo(AnsiColor.Default);
        await Assert.That(style.Background).IsEqualTo(AnsiColor.Default);
        await Assert.That(style.Bold).IsFalse();
    }

    [Test]
    public async Task IndexedColourCarriesItsIndex()
    {
        var colour = AnsiColor.Indexed(200);

        await Assert.That(colour.Kind).IsEqualTo(AnsiColorKind.Indexed);
        await Assert.That(colour.Index).IsEqualTo(200);
    }
}
