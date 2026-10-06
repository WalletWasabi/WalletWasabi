namespace WalletWasabi.Mobile;

public enum RuntimeLifecycle { Stopped, Starting, Ready, Reconnecting, Stopping, Failed }
public sealed record RuntimeStatus(RuntimeLifecycle Lifecycle, int TorBootstrap, int Peers, bool Synchronized, string CoinJoin, int PendingSubmissions, string? Error, int PendingCoinJoins = 0, SynchronizationSnapshot? Synchronization = null);
