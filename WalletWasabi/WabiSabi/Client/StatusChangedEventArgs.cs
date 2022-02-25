using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class StatusChangedEventArgs : EventArgs
{
	public Wallet Wallet { get; }

	public StatusChangedEventArgs(Wallet wallet)
	{
		Wallet = wallet;
	}
}

public class LoadedEventArgs : StatusChangedEventArgs
{
	public TimeSpan AutoStartDelay { get; }
	public LoadedEventArgs(Wallet wallet, TimeSpan autoStartDelay)
		: base(wallet)
	{
		AutoStartDelay = autoStartDelay;
	}
}

public class StartedEventArgs : StatusChangedEventArgs
{
	public StartedEventArgs(Wallet wallet)
		: base(wallet)
	{
	}
}

public class CoinJoinCompletedEventArgs : StatusChangedEventArgs
{
	public CompletionStatus CompletionStatus { get; }

	public CoinJoinCompletedEventArgs(Wallet wallet, CompletionStatus completionStatus)
		: base(wallet)
	{
		CompletionStatus = completionStatus;
	}
}

public class StopedEventArgs : StatusChangedEventArgs
{
	public StopReason Reason { get; }
	public StopedEventArgs(Wallet wallet, StopReason reason)
		: base(wallet)
	{
		Reason = reason;
	}
}

public class StartErrorEventArgs : StatusChangedEventArgs
{
	public CoinjoinError Error;

	public StartErrorEventArgs(Wallet wallet, CoinjoinError error)
		: base(wallet)
	{
		Error = error;
	}
}

public enum StopReason
{
	WalletUnloaded
}

public enum CompletionStatus
{
	Success,
	Canceled,
	Failed,
	Unknown
}

public enum CoinjoinError
{
	NoCoinsToMix
}
