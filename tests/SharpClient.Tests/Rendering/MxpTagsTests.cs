using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class MxpTagsTests
{
    [Test]
    public async Task EmptyRequestListsEverySupportedTag()
    {
        var (supported, unsupported) = MxpTags.Answer([]);
        await Assert.That(supported).Contains("send").And.Contains("image").And.Contains("color").And.Contains("a");
        await Assert.That(supported).DoesNotContain("frame").And.DoesNotContain("sound");
        await Assert.That(unsupported).IsEmpty();
    }

    [Test]
    public async Task TagsAreSortedIntoSupportedAndNot()
    {
        var (supported, unsupported) = MxpTags.Answer(["SEND", "frame", "image"]);
        await Assert.That(string.Join(",", supported)).IsEqualTo("SEND,image");
        await Assert.That(string.Join(",", unsupported)).IsEqualTo("frame");
    }

    [Test]
    public async Task AttributesAreAnsweredPerAttribute()
    {
        var (supported, unsupported) = MxpTags.Answer(["send.href", "send.prompt", "image.url", "image.t", "color.fore", "font.face"]);
        await Assert.That(string.Join(",", supported)).IsEqualTo("send.href,image.url,image.t,color.fore");
        await Assert.That(string.Join(",", unsupported)).IsEqualTo("send.prompt,font.face");
    }

    [Test]
    public async Task WildcardExpandsForSupportedTagOnly()
    {
        var (supported, unsupported) = MxpTags.Answer(["send.*", "frame.*"]);
        await Assert.That(string.Join(",", supported)).IsEqualTo("send.href,send.hint");
        await Assert.That(string.Join(",", unsupported)).IsEqualTo("frame.*");
    }

    [Test]
    public async Task QuotedEntriesAreUnquoted()
    {
        var (supported, unsupported) = MxpTags.Answer(["\"send\"", "''"]);
        await Assert.That(string.Join(",", supported)).IsEqualTo("send");
        await Assert.That(unsupported).IsEmpty();
    }
}
