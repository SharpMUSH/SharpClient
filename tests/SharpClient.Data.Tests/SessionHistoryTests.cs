using SharpClient.Data;

namespace SharpClient.Data.Tests;

public sealed class SessionHistoryTests
{
    private TempFileAppStorage _storage = null!;

    [Before(Test)]
    public void Setup() => _storage = new TempFileAppStorage();

    [After(Test)]
    public void Teardown() => _storage.Delete();

    // ── Append + Search ──────────────────────────────────────────────────────

    [Test]
    public async Task AppendThenSearchReturnsMatchingLinesWithCorrectCharacterId()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();

        await history.AppendAsync(charId, "the quick brown fox");
        await history.AppendAsync(charId, "jumps over the lazy dog");
        await history.AppendAsync(charId, "unrelated text here");

        var hits = await history.SearchAsync("fox");

        await Assert.That(hits).Count().IsEqualTo(1);
        await Assert.That(hits[0].CharacterId).IsEqualTo(charId);
        await Assert.That(hits[0].Line).IsEqualTo("the quick brown fox");
    }

    // ── Multi-word search ────────────────────────────────────────────────────

    [Test]
    public async Task MultiWordSearchMatchesLinesContainingAllTerms()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();

        await history.AppendAsync(charId, "quick brown fox leaps");
        await history.AppendAsync(charId, "the fox is quick");
        await history.AppendAsync(charId, "the quick dog runs");

        // "quick" AND "fox" → first two lines match; third does not
        var hits = await history.SearchAsync("quick fox");

        await Assert.That(hits).Count().IsEqualTo(2);
        await Assert.That(hits.Any(h => h.Line == "quick brown fox leaps")).IsTrue();
        await Assert.That(hits.Any(h => h.Line == "the fox is quick")).IsTrue();
    }

    // ── Multi-character search ────────────────────────────────────────────────

    [Test]
    public async Task SearchAcrossTwoCharactersReturnsCorrectCharacterIds()
    {
        var history = new SessionHistory(_storage);
        var charA = Guid.NewGuid();
        var charB = Guid.NewGuid();

        await history.AppendAsync(charA, "hello world from alpha");
        await history.AppendAsync(charB, "hello world from beta");
        await history.AppendAsync(charA, "goodbye alpha only");

        var hits = await history.SearchAsync("hello");

        await Assert.That(hits).Count().IsEqualTo(2);
        var hitForA = hits.Single(h => h.Line.Contains("alpha"));
        var hitForB = hits.Single(h => h.Line.Contains("beta"));
        await Assert.That(hitForA.CharacterId).IsEqualTo(charA);
        await Assert.That(hitForB.CharacterId).IsEqualTo(charB);
    }

    // ── Limit ────────────────────────────────────────────────────────────────

    [Test]
    public async Task LimitIsRespectedWhenMoreMatchesExist()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
            await history.AppendAsync(charId, $"matching line number {i}");

        var hits = await history.SearchAsync("matching", limit: 3);

        await Assert.That(hits).Count().IsEqualTo(3);
    }

    // ── No-match / empty ─────────────────────────────────────────────────────

    [Test]
    public async Task SearchWithNoMatchReturnsEmptyList()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();

        await history.AppendAsync(charId, "some line about things");

        var hits = await history.SearchAsync("zzznomatchzzz");

        await Assert.That(hits).Count().IsEqualTo(0);
    }

    [Test]
    public async Task EmptyOrWhitespaceQueryReturnsEmptyListWithoutException()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();
        await history.AppendAsync(charId, "some line");

        var hitsEmpty = await history.SearchAsync("");
        var hitsWhitespace = await history.SearchAsync("   \t ");

        await Assert.That(hitsEmpty).Count().IsEqualTo(0);
        await Assert.That(hitsWhitespace).Count().IsEqualTo(0);
    }

    // ── FTS-special characters ───────────────────────────────────────────────

    [Test]
    public async Task QueryWithFtsSpecialCharactersDoesNotThrow()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();
        await history.AppendAsync(charId, "a line with ordinary content");

        // These would cause FTS5 parse errors if not sanitised:
        //   "unclosed  → unmatched double-quote
        //   *          → bare prefix operator
        //   """        → malformed quoting
        var result1 = await history.SearchAsync("\"unclosed");
        var result2 = await history.SearchAsync("*");
        var result3 = await history.SearchAsync("\"\"\"");

        // Results are empty (special chars don't match plain text), but no exception was raised
        await Assert.That(result1).IsNotNull();
        await Assert.That(result2).IsNotNull();
        await Assert.That(result3).IsNotNull();
    }

    // ── Sequence is monotonically from rowid ─────────────────────────────────

    [Test]
    public async Task SequenceValuesArePositiveAndDistinctPerAppend()
    {
        var history = new SessionHistory(_storage);
        var charId = Guid.NewGuid();

        await history.AppendAsync(charId, "first apple line");
        await history.AppendAsync(charId, "second apple line");
        await history.AppendAsync(charId, "third apple line");

        var hits = await history.SearchAsync("apple", limit: 10);

        await Assert.That(hits).Count().IsEqualTo(3);

        var sequences = hits.Select(h => h.Sequence).ToList();
        // All sequences are positive (FTS5 rowids start at 1)
        await Assert.That(sequences.All(s => s > 0)).IsTrue();
        // All sequences are distinct
        await Assert.That(sequences.Distinct().Count()).IsEqualTo(3);
    }
}
