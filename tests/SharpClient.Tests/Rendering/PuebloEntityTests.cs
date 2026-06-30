using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

/// <summary>
/// HTML-entity handling for Pueblo/MXP markup. In Pueblo HTML mode the server entity-encodes the
/// reserved characters (e.g. a literal '&gt;' is sent as "&amp;gt;"), so the client must decode them
/// on render — but only inside markup mode, and a lone/unknown '&amp;' must pass through literally.
/// </summary>
public sealed class PuebloEntityTests
{
    private static MxpParserState Pueblo() => new() { IsPuebloActive = true };

    private static string Render(string line, MxpParserState? mxp)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var seg in AnsiParser.Parse(line, mxp))
        {
            sb.Append(seg.Text);
        }

        return sb.ToString();
    }

    [Test]
    public async Task DecodesCoreEntitiesInPuebloMode()
    {
        await Assert.That(Render("5 &gt; 3 &amp;&amp; 2 &lt; 4 &quot;q&quot;", Pueblo()))
            .IsEqualTo("5 > 3 && 2 < 4 \"q\"");
    }

    [Test]
    public async Task DecodesAposAndNbsp()
    {
        await Assert.That(Render("it&apos;s&nbsp;here", Pueblo())).IsEqualTo("it's here");
    }

    [Test]
    public async Task DecodesNumericDecimalAndHex()
    {
        await Assert.That(Render("&#62;&#x3E;&#38;", Pueblo())).IsEqualTo(">>&");
    }

    [Test]
    public async Task IgnoresControlRangeNumericEntities()
    {
        // U+0007 (BEL) is below U+0020: consumed but produces no output (matches MXP behaviour).
        await Assert.That(Render("a&#7;b", Pueblo())).IsEqualTo("ab");
    }

    [Test]
    public async Task LoneAmpersandPassesThroughLiterally()
    {
        await Assert.That(Render("Tom & Jerry", Pueblo())).IsEqualTo("Tom & Jerry");
    }

    [Test]
    public async Task UnknownEntityPassesThroughLiterally()
    {
        await Assert.That(Render("x&notathing;y", Pueblo())).IsEqualTo("x&notathing;y");
    }

    [Test]
    public async Task DoesNotDecodeEntitiesOutsideMarkupMode()
    {
        // No MXP/Pueblo active: '&' and entities are plain text, never decoded.
        await Assert.That(Render("5 &gt; 3", null)).IsEqualTo("5 &gt; 3");
    }

    [Test]
    public async Task DecodesEntitiesInMxpMode()
    {
        await Assert.That(Render("5 &gt; 3", new MxpParserState { IsMxpActive = true }))
            .IsEqualTo("5 > 3");
    }

    [Test]
    public async Task EntityInsideAttributeValueIsDecoded()
    {
        var segs = AnsiParser.Parse("<a xch_cmd=\"say 5 &gt; 3\">go</a>", Pueblo());

        await Assert.That(segs.Count).IsEqualTo(1);
        await Assert.That(segs[0].Text).IsEqualTo("go");
        await Assert.That(segs[0].Command).IsEqualTo("say 5 > 3");
    }

    [Test]
    public async Task RawGreaterThanInsideQuotedAttributeDoesNotTruncateTag()
    {
        // A literal '>' inside a quoted attribute must not end the tag early.
        var segs = AnsiParser.Parse("<a xch_cmd=\"say 5 > 3\">go</a>", Pueblo());

        await Assert.That(segs.Count).IsEqualTo(1);
        await Assert.That(segs[0].Text).IsEqualTo("go");
        await Assert.That(segs[0].Command).IsEqualTo("say 5 > 3");
    }
}
