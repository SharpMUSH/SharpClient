using SharpClient.Core.Rendering;

namespace SharpClient.Tests.Rendering;

public sealed class MxpTagsTests
{
    [Test]
    public async Task EmptyRequestListsEverySupportedTag()
    {
        var (supported, unsupported) = MxpTags.Answer([]);
        await Assert.That(string.Join(",", supported)).IsEqualTo("send,image");
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
        var (supported, unsupported) = MxpTags.Answer(["send.href", "send.prompt", "image.url", "color.fore"]);
        await Assert.That(string.Join(",", supported)).IsEqualTo("send.href,image.url");
        await Assert.That(string.Join(",", unsupported)).IsEqualTo("send.prompt,color.fore");
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
