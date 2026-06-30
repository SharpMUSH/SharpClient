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

    // ── IWorldStore ──────────────────────────────────────────────────────────

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
            await _db.SaveChangesAsync(cancellationToken); // commit deletion first
        }

        // Clear all tracked entities after the deletion so the subsequent Add
        // does not re-use stale tracked instances for the incoming graph.
        _db.ChangeTracker.Clear();

        _db.Worlds.Add(world);
        await _db.SaveChangesAsync(cancellationToken); // then commit insertion
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured) return;
        await _db.Database.EnsureCreatedAsync(cancellationToken);
        _schemaEnsured = true;
    }
}
