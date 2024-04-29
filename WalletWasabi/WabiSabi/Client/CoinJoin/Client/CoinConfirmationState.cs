using System.Threading;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

/*
 * Used by CoinJoinClient.RegisterInputAsync to store the common data/tokens during the coin confirmation phase.
 */

internal class CoinConfirmationState : IDisposable
{
	public CoinConfirmationState(RoundState roundState, CancellationToken cancel, TimeSpan extraPhaseTimeoutMargin)
	{
		RoundState = roundState;
		Cancel = cancel;
		LastUnexpectedRoundPhaseException = null;

		var remainingInputRegTime = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;

		StrictInputRegTimeoutCts = new(remainingInputRegTime);
		InputRegTimeoutCts = new(remainingInputRegTime + extraPhaseTimeoutMargin);
		ConnConfTimeoutCts = new(remainingInputRegTime + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout + extraPhaseTimeoutMargin);
		RegistrationsCts = new();
		ConfirmationsCts = new();

		LinkedUnregisterCts = CancellationTokenSource.CreateLinkedTokenSource(StrictInputRegTimeoutCts.Token, RegistrationsCts.Token);
		LinkedRegistrationsCts = CancellationTokenSource.CreateLinkedTokenSource(InputRegTimeoutCts.Token, RegistrationsCts.Token, cancel);
		LinkedConfirmationsCts = CancellationTokenSource.CreateLinkedTokenSource(ConnConfTimeoutCts.Token, ConfirmationsCts.Token, cancel);
		TimeoutAndGlobalCts = CancellationTokenSource.CreateLinkedTokenSource(InputRegTimeoutCts.Token, ConnConfTimeoutCts.Token, cancel);
	}

	public int EventInvocedAlready(int set)
	{
		return Interlocked.Exchange(ref _eventInvokedAlready, 1);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				TimeoutAndGlobalCts.Dispose();
				LinkedConfirmationsCts.Dispose();
				LinkedRegistrationsCts.Dispose();
				LinkedUnregisterCts.Dispose();
				ConfirmationsCts.Dispose();
				RegistrationsCts.Dispose();
				ConnConfTimeoutCts.Dispose();
				InputRegTimeoutCts.Dispose();
				StrictInputRegTimeoutCts.Dispose();
			}
			_disposed = true;
		}
	}

	private bool _disposed = false;
	private int _eventInvokedAlready = 0;

	public RoundState RoundState { get; init; }
	public CancellationToken Cancel { get; init; }

	public UnexpectedRoundPhaseException? LastUnexpectedRoundPhaseException { get; set; }

	public CancellationTokenSource StrictInputRegTimeoutCts { get; init; }
	public CancellationTokenSource InputRegTimeoutCts { get; init; }
	public CancellationTokenSource ConnConfTimeoutCts { get; init; }
	public CancellationTokenSource RegistrationsCts { get; init; }
	public CancellationTokenSource ConfirmationsCts { get; init; }

	public CancellationTokenSource LinkedUnregisterCts { get; init; }
	public CancellationTokenSource LinkedRegistrationsCts { get; init; }
	public CancellationTokenSource LinkedConfirmationsCts { get; init; }
	public CancellationTokenSource TimeoutAndGlobalCts { get; init; }
}
