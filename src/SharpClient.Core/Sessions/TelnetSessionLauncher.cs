using SharpClient.Core.Connection;
using SharpClient.Core.Domain;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;
using SharpClient.Core.Triggers;

namespace SharpClient.Core.Sessions;

/// <summary>
/// Real <see cref="ISessionLauncher"/> shared by both the Blazor Server web host
/// and the MAUI Blazor Hybrid Android host. Opens a TCP telnet connection via
/// <see cref="ITelnetConnectionFactory"/> (which wraps TelnetNegotiationCore),
/// wraps it in a <see cref="Session"/>, and auto-sends the character's connect
/// string (resolved from <see cref="ISecretStore"/>) immediately after connecting.
/// Disposes the session if the secret-send fails.
/// Engines and history are injected from DI and threaded into each Session built here.
/// </summary>
public sealed class TelnetSessionLauncher : ISessionLauncher
{
    private readonly ITelnetConnectionFactory _connFactory;
    private readonly ISecretStore _secrets;
    private readonly IAliasEngine _aliasEngine;
    private readonly ITriggerEngine _triggerEngine;
    private readonly INotifier _notifier;
    private readonly ISessionHistory _history;

    public TelnetSessionLauncher(
        ITelnetConnectionFactory connFactory,
        ISecretStore secrets,
        IAliasEngine aliasEngine,
        ITriggerEngine triggerEngine,
        INotifier notifier,
        ISessionHistory history)
    {
        _connFactory = connFactory;
        _secrets = secrets;
        _aliasEngine = aliasEngine;
        _triggerEngine = triggerEngine;
        _notifier = notifier;
        _history = history;
    }

    public async Task<ISession> LaunchAsync(
        World world,
        Character character,
        CancellationToken cancellationToken = default)
    {
        var connection = _connFactory.CreateConnection();

        var aliasRules = MergeAliases(world.Aliases, character.Aliases);
        var triggerRules = MergeTriggers(world.Triggers, character.Triggers);

        var session = new Session(
            connection,
            character.Name,
            world.Name,
            world.Id,
            character.Id,
            _aliasEngine,
            aliasRules,
            _triggerEngine,
            triggerRules,
            _notifier,
            _history);

        await session.ConnectAsync(world.Host, world.Port, cancellationToken);

        try
        {
            if (character.ConnectSecretKey is { } key)
            {
                var secret = await _secrets.GetAsync(key);
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    await session.SendAsync(secret);
                }
            }
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }

        return session;
    }

    // Merges world and character alias lists; character wins on duplicate Pattern.
    private static IReadOnlyList<AliasRule> MergeAliases(
        IReadOnlyList<AliasRule> worldAliases,
        IReadOnlyList<AliasRule> characterAliases)
    {
        var merged = new Dictionary<string, AliasRule>(StringComparer.Ordinal);
        foreach (var rule in worldAliases)
        {
            merged[rule.Pattern] = rule;
        }

        foreach (var rule in characterAliases)
        {
            merged[rule.Pattern] = rule;
        }

        return [.. merged.Values];
    }

    // Merges world and character trigger lists; character wins on duplicate Pattern.
    private static IReadOnlyList<TriggerRule> MergeTriggers(
        IReadOnlyList<TriggerRule> worldTriggers,
        IReadOnlyList<TriggerRule> characterTriggers)
    {
        var merged = new Dictionary<string, TriggerRule>(StringComparer.Ordinal);
        foreach (var rule in worldTriggers)
        {
            merged[rule.Pattern] = rule;
        }

        foreach (var rule in characterTriggers)
        {
            merged[rule.Pattern] = rule;
        }

        return [.. merged.Values];
    }
}
