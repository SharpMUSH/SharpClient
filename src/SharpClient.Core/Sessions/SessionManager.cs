namespace SharpClient.Core.Sessions;

public sealed class SessionManager : ISessionManager
{
    private readonly List<ISession> _sessions = [];

    public IReadOnlyList<ISession> Sessions => _sessions;

    public ISession? Active { get; private set; }

    public event Action? Changed;

    public void Add(ISession session)
    {
        _sessions.Add(session);
        Active = session;
        Changed?.Invoke();
    }

    public void Activate(ISession session)
    {
        if (!_sessions.Contains(session))
        {
            return;
        }

        Active = session;
        Changed?.Invoke();
    }

    public async Task CloseAsync(ISession session)
    {
        if (!_sessions.Remove(session))
        {
            return;
        }

        if (Active == session)
        {
            Active = _sessions.Count > 0 ? _sessions[0] : null;
        }

        await session.DisposeAsync();
        Changed?.Invoke();
    }
}
