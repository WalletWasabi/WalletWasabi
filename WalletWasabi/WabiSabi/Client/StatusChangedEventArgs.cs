using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class StatusChangedEventArgs : EventArgs
{
	public StatusChangedEventArgs(Wallet wallet)
	{
		Wallet = wallet;
	}

	public Wallet Wallet { get; }
}

public class LoadedEventArgs : StatusChangedEventArgs
{
	public LoadedEventArgs(Wallet wallet)
		: base(wallet)
	{
	}
}

public class StartedEventArgs : StatusChangedEventArgs
{
	public StartedEventArgs(Wallet wallet, TimeSpan registrationTimeout)
		: base(wallet)
	{
		RegistrationTimeout = registrationTimeout;
	}

	public TimeSpan RegistrationTimeout { get; }
}

public class CompletedEventArgs : StatusChangedEventArgs
{
	public CompletedEventArgs(Wallet wallet, CompletionStatus completionStatus)
		: base(wallet)
	{
		CompletionStatus = completionStatus;
	}

	public CompletionStatus CompletionStatus { get; }
}

public class StoppedEventArgs : StatusChangedEventArgs
{
	public StoppedEventArgs(Wallet wallet, StopReason reason)
		: base(wallet)
	{
		Reason = reason;
	}

	public StopReason Reason { get; }
}

public class StartErrorEventArgs : StatusChangedEventArgs
{
	public StartErrorEventArgs(Wallet wallet, CoinjoinError error)
		: base(wallet)
	{
		Error = error;
	}

	public CoinjoinError Error { get; }
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
	NoCoinsToMix,
	AutoConjoinDisabled,
	UserInSendWorkflow,
	NotEnoughUnprivateBalance
}
