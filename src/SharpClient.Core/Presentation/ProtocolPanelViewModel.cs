using SharpClient.Core.Connection;
using SharpClient.Core.Sessions;

namespace SharpClient.Core.Presentation;

public sealed class ProtocolPanelViewModel : IDisposable
{
    private readonly ISessionManager _manager;
    private ISession? _trackedSession;

    public ProtocolPanelViewModel(ISessionManager manager)
    {
        _manager = manager;
        _manager.Changed += OnManagerChanged;
        UpdateTrackedSession();
    }

    public IReadOnlyList<NegotiationEvent> NegotiationLog =>
        _manager.Active?.NegotiationLog ?? [];

    public IReadOnlyList<GmcpMessage> GmcpLog =>
        _manager.Active?.GmcpLog ?? [];

    public event Action? Changed;

    public void Dispose()
    {
        _manager.Changed -= OnManagerChanged;
        DetachSession(_trackedSession);
    }

    private void OnManagerChanged()
    {
        UpdateTrackedSession();
        Changed?.Invoke();
    }

    private void UpdateTrackedSession()
    {
        var next = _manager.Active;
        if (next == _trackedSession)
        {
            return;
        }

        DetachSession(_trackedSession);
        _trackedSession = next;
        if (_trackedSession is not null)
        {
            _trackedSession.ProtocolChanged += OnProtocolChanged;
        }
    }

    private void DetachSession(ISession? session)
    {
        if (session is not null)
        {
            session.ProtocolChanged -= OnProtocolChanged;
        }
    }

    private void OnProtocolChanged() => Changed?.Invoke();
}
