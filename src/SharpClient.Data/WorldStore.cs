using Microsoft.EntityFrameworkCore;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;

namespace SharpClient.Data;

/// <summary>
/// EF Core SQLite implementation of <see cref="IWorldStore"/>.
/// </summary>
public sealed class WorldStore : IWorldStore
{
    private readonly AppDbContext _db;
    private bool _schemaEnsured;

    public WorldStore(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        return await _db.Worlds
            .Include(w => w.Characters)
                .ThenInclude(c => c.Triggers)
            .Include(w => w.Characters)
                .ThenInclude(c => c.Aliases)
            .Include(w => w.Triggers)
            .Include(w => w.Aliases)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        _db.Worlds.Add(world);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Update strategy: load the existing world graph fully, remove it, commit the
    /// deletion, then add the incoming graph and commit. Two round-trips are used
    /// to avoid a UNIQUE constraint violation that occurs when EF batches a same-PK
    /// DELETE and INSERT into one SQLite statement batch (INSERT executes before DELETE).
    /// This approach handles added/removed/modified children correctly without any
    /// diff-and-patch graph tracking.
    /// </summary>
    public async Task UpdateWorldAsync(World world, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        // Atomic boundary: the delete and re-insert must commit together, or a
        // failure between them would leave the world permanently deleted.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Clear stale tracked entities so identity resolution returns a clean load.
        _db.ChangeTracker.Clear();

        // Load the existing graph so EF's client-side cascade marks all children for deletion.
        var existing = await _db.Worlds
            .Include(w => w.Characters)
                .ThenInclude(c => c.Triggers)
            .Include(w => w.Characters)
                .ThenInclude(c => c.Aliases)
            .Include(w => w.Triggers)
            .Include(w => w.Aliases)
            .FirstOrDefaultAsync(w => w.Id == world.Id, cancellationToken);

        if (existing is not null)
        {
            _db.Worlds.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken); // delete within the transaction
        }

        // Clear all tracked entities after the deletion so the subsequent Add
        // does not re-use stale tracked instances for the incoming graph.
        _db.ChangeTracker.Clear();

        _db.Worlds.Add(world);
        await _db.SaveChangesAsync(cancellationToken); // re-insert within the transaction

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteWorldAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        var existing = await _db.Worlds
            .Include(w => w.Characters)
                .ThenInclude(c => c.Triggers)
            .Include(w => w.Characters)
                .ThenInclude(c => c.Aliases)
            .Include(w => w.Triggers)
            .Include(w => w.Aliases)
            .FirstOrDefaultAsync(w => w.Id == worldId, cancellationToken);

        if (existing is not null)
        {
            _db.Worlds.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured) return;
        await _db.Database.EnsureCreatedAsync(cancellationToken);
        await AddMissingColumnsAsync(cancellationToken);
        _schemaEnsured = true;
    }

    // The schema comes from EnsureCreated, which does nothing to a database that already exists, so a
    // column added to the model after a user's database was created has to be added here. Each entry
    // is NOT NULL with a default, so existing rows read as they did before the column existed.
    private static readonly (string Table, string Column, string Definition)[] s_addedColumns =
    [
        ("Worlds", nameof(World.UseTls), "INTEGER NOT NULL DEFAULT 0"),
        ("Worlds", nameof(World.AllowInvalidCertificate), "INTEGER NOT NULL DEFAULT 0"),
    ];

    private async Task AddMissingColumnsAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var group in s_addedColumns.GroupBy(c => c.Table))
            {
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var query = connection.CreateCommand())
                {
                    // Table names are the constants above, never input.
#pragma warning disable CA2100
                    query.CommandText = $"PRAGMA table_info(\"{group.Key}\")";
#pragma warning restore CA2100
                    await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        existing.Add(reader.GetString(1));
                    }
                }

                foreach (var (table, column, definition) in group)
                {
                    if (existing.Contains(column))
                    {
                        continue;
                    }

                    await using var alter = connection.CreateCommand();
#pragma warning disable CA2100
                    alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
#pragma warning restore CA2100
                    try
                    {
                        await alter.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch (System.Data.Common.DbException ex)
                        when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                    {
                        // Another store on the same database added it between the check and here.
                    }
                }
            }
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
