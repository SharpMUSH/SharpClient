namespace SharpClient.Core.Sessions;

public interface ISessionManager
{
    public IReadOnlyList<ISession> Sessions { get; }

    public ISession? Active { get; }

    public event Action? Changed;

    public void Add(ISession session);

    public void Activate(ISession session);

    public Task CloseAsync(ISession session);
}
