namespace WalletWasabi.Mobile;

// Background decisions are queued for the Android main thread. Bind each one to
// the activity state and runtime that existed when it was requested.
public sealed class RuntimeForegroundState
{
    private readonly object _gate = new();
    private bool _foreground;
    private long _generation;

    public bool IsForeground
    {
        get { lock (_gate) { return _foreground; } }
        set
        {
            lock (_gate)
            {
                if (_foreground == value) { return; }
                _foreground = value;
                _generation++;
            }
        }
    }

    public StopTicket? CaptureIdleStop(object? runtime)
    {
        lock (_gate) { return !_foreground && runtime is not null ? new(_generation, runtime) : null; }
    }

    public bool IsCurrent(StopTicket ticket, object? runtime)
    {
        lock (_gate) { return !_foreground && ticket.Generation == _generation && ReferenceEquals(ticket.Runtime, runtime); }
    }

    public sealed class StopTicket
    {
        internal StopTicket(long generation, object runtime) { Generation = generation; Runtime = runtime; }
        internal long Generation { get; }
        internal object Runtime { get; }
    }
}
