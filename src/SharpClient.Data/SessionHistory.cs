using Microsoft.Data.Sqlite;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;

namespace SharpClient.Data;

/// <summary>
/// SQLite FTS5 implementation of <see cref="ISessionHistory"/>.
/// Stores session lines in a virtual FTS5 table on the same database file used by
/// <see cref="AppDbContext"/>. The FTS5 implicit <c>rowid</c> is used as the
/// monotonic <see cref="HistoryHit.Sequence"/> value returned by searches.
/// </summary>
public sealed class SessionHistory : ISessionHistory
{
    private readonly string _dbPath;

    public SessionHistory(IAppStorage storage)
    {
        _dbPath = storage.GetDatabasePath();
    }

    // ── ISessionHistory ──────────────────────────────────────────────────────

    public async Task AppendAsync(Guid characterId, string line, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO session_history(character_id, line) VALUES(@cid, @line)";
        cmd.Parameters.AddWithValue("@cid", characterId.ToString());
        cmd.Parameters.AddWithValue("@line", line);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HistoryHit>> SearchAsync(
        string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        var sanitised = SanitiseFtsQuery(query);
        if (sanitised is null) return [];

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT character_id, line, rowid FROM session_history WHERE session_history MATCH @q ORDER BY rank LIMIT @limit";
        cmd.Parameters.AddWithValue("@q", sanitised);
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<HistoryHit>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var characterId = Guid.Parse(reader.GetString(0));
            var line = reader.GetString(1);
            var sequence = reader.GetInt64(2);
            results.Add(new HistoryHit(characterId, line, sequence));
        }

        return results;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the FTS5 table if it does not yet exist.
    /// <c>CREATE VIRTUAL TABLE IF NOT EXISTS</c> is idempotent so this is safe to
    /// call on every operation without tracking state.
    /// </summary>
    /// <remarks>
    /// The table has no explicit <c>sequence</c> column; ordering is derived from
    /// FTS5's implicit <c>rowid</c>, which is a monotonically increasing integer
    /// assigned at insert time and exposed as <see cref="HistoryHit.Sequence"/> by
    /// <see cref="SearchAsync"/>.
    /// </remarks>
    private static async Task EnsureTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "CREATE VIRTUAL TABLE IF NOT EXISTS session_history USING fts5(character_id UNINDEXED, line);";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Converts free-form user text into a safe FTS5 query expression.
    /// Each whitespace-delimited token has embedded <c>"</c> characters doubled and is
    /// then wrapped in double quotes, producing a conjunction of phrase literals.
    /// Returns <see langword="null"/> for empty or whitespace-only input so the caller
    /// can skip the query entirely.
    /// </summary>
    /// <example>
    /// <c>"foo \"bar"</c> → <c>"\"foo\" \"\"bar\""</c>
    /// (FTS5 literals: "foo" and ""bar, i.e. the literal double-quote followed by bar)
    /// </example>
    private static string? SanitiseFtsQuery(string query)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return null;

        return string.Join(" ", tokens.Select(t => "\"" + t.Replace("\"", "\"\"") + "\""));
    }
}
