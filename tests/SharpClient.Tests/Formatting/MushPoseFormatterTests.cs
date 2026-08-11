using SharpClient.Core.Formatting;

namespace SharpClient.Tests.Formatting;

public sealed class MushPoseFormatterTests
{
    [Test]
    public async Task LiteralPercentIsDoubled()
    {
        var result = MushPoseFormatter.Format("pose", "is 100% sure");
        await Assert.That(result).IsEqualTo("pose is 100%% sure");
    }

    [Test]
    public async Task NewlineBecomesPercentR()
    {
        var result = MushPoseFormatter.Format("pose", "line one\nline two");
        await Assert.That(result).IsEqualTo("pose line one%rline two");
    }

    [Test]
    public async Task PercentEscapingRunsBeforeNewlineSubstitution()
    {
        var result = MushPoseFormatter.Format("pose", "50%\nrest");
        await Assert.That(result).IsEqualTo("pose 50%%%rrest");
    }

    [Test]
    public async Task CarriageReturnLineFeedNormalisesLikeLineFeed()
    {
        var result = MushPoseFormatter.Format("pose", "a\r\nb");
        await Assert.That(result).IsEqualTo("pose a%rb");
    }

    [Test]
    public async Task LoneCarriageReturnNormalisesLikeLineFeed()
    {
        var result = MushPoseFormatter.Format("pose", "a\rb");
        await Assert.That(result).IsEqualTo("pose a%rb");
    }

    [Test]
    public async Task InteriorBlankLineBecomesTwoPercentR()
    {
        var result = MushPoseFormatter.Format("pose", "a\n\nb");
        await Assert.That(result).IsEqualTo("pose a%r%rb");
    }

    [Test]
    public async Task TrailingBlankLinesAreDropped()
    {
        var result = MushPoseFormatter.Format("pose", "a\n\n\n");
        await Assert.That(result).IsEqualTo("pose a");
    }

    [Test]
    public async Task LeadingBlankLinesAreDropped()
    {
        var result = MushPoseFormatter.Format("pose", "\n\na");
        await Assert.That(result).IsEqualTo("pose a");
    }

    [Test]
    public async Task TrailingWhitespaceIsTrimmedPerLine()
    {
        var result = MushPoseFormatter.Format("pose", "a   \nb\t");
        await Assert.That(result).IsEqualTo("pose a%rb");
    }

    [Test]
    public async Task LeadingIndentOnALineIsPreserved()
    {
        var result = MushPoseFormatter.Format("pose", "a\n   b");
        await Assert.That(result).IsEqualTo("pose a%r   b");
    }

    [Test]
    public async Task BracketsAndBackslashesPassThroughUnescaped()
    {
        var result = MushPoseFormatter.Format("pose", @"holds [a] \ thing");
        await Assert.That(result).IsEqualTo(@"pose holds [a] \ thing");
    }

    [Test]
    public async Task PrefixEndingInEqualsJoinsWithoutSpace()
    {
        var result = MushPoseFormatter.Format("page Bob=", "hello");
        await Assert.That(result).IsEqualTo("page Bob=hello");
    }

    [Test]
    public async Task PrefixEndingInSlashJoinsWithoutSpace()
    {
        var result = MushPoseFormatter.Format("chan/", "hello");
        await Assert.That(result).IsEqualTo("chan/hello");
    }

    [Test]
    public async Task PrefixEndingInSpaceJoinsVerbatim()
    {
        var result = MushPoseFormatter.Format("page Bob ", "hello");
        await Assert.That(result).IsEqualTo("page Bob hello");
    }

    [Test]
    public async Task BarePrefixGetsASingleSpace()
    {
        var result = MushPoseFormatter.Format("@emit", "hello");
        await Assert.That(result).IsEqualTo("@emit hello");
    }

    [Test]
    public async Task EmptyBodyYieldsPrefixOnly()
    {
        var result = MushPoseFormatter.Format("pose", string.Empty);
        await Assert.That(result).IsEqualTo("pose");
    }

    [Test]
    public async Task WhitespaceOnlyBodyYieldsPrefixOnly()
    {
        var result = MushPoseFormatter.Format("page Bob=", "  \n \n ");
        await Assert.That(result).IsEqualTo("page Bob=");
    }

    [Test]
    public async Task EmptyPrefixYieldsBodyOnly()
    {
        var result = MushPoseFormatter.Format(string.Empty, "hello");
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task CommandForMapsEveryBuiltIn()
    {
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Say, "x")).IsEqualTo("say");
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Pose, "x")).IsEqualTo("pose");
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Semipose, "x")).IsEqualTo("semipose");
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Emit, "x")).IsEqualTo("@emit");
    }

    [Test]
    public async Task CommandForCustomReturnsTheCustomPrefix()
    {
        await Assert.That(MushPoseFormatter.CommandFor(PosePrefix.Custom, "page Bob=")).IsEqualTo("page Bob=");
    }
}
