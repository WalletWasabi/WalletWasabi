using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinProgressEventArgs : EventArgs
{
}

public class WaitingForRound : CoinJoinProgressEventArgs
{
}

public class WaitingForBlameRound : CoinJoinProgressEventArgs
{
	public WaitingForBlameRound(DateTimeOffset dateTimeOffset)
	{
		DateTimeOffset = dateTimeOffset;
	}

	public DateTimeOffset DateTimeOffset { get; }
}

public class RoundStateChanged : CoinJoinProgressEventArgs
{
	public RoundStateChanged(RoundState roundState, DateTimeOffset timeout)
	{
		RoundState = roundState;
		Timeout = timeout;
	}

	public RoundState RoundState { get; }
	public DateTimeOffset Timeout { get; }
}

public class EnteringInputRegistrationPhase : RoundStateChanged
{
	public EnteringInputRegistrationPhase(RoundState roundState, DateTimeOffset timeout) : base(roundState, timeout)
	{
	}
}

public class EnteringOutputRegistrationPhase : RoundStateChanged
{
	public EnteringOutputRegistrationPhase(RoundState roundState, DateTimeOffset timeout) : base(roundState, timeout)
	{
	}
}

public class EnteringSigningPhase : RoundStateChanged
{
	public EnteringSigningPhase(RoundState roundState, DateTimeOffset timeout) : base(roundState, timeout)
	{
	}
}

public class RoundEnded : CoinJoinProgressEventArgs
{
	public RoundEnded(RoundState roundState)
	{
		RoundState = roundState;
	}

	public RoundState RoundState { get; }
}
