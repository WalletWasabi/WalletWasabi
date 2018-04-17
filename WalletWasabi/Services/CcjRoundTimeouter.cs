using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
    public class CcjRoundTimeouter : IDisposable
	{
		public CcjRound Round { get; }
		public CcjRoundPhase PhaseToTimeout { get; }
		public TimeSpan Timeout { get; }

		/// <summary>
		/// 0: No, 1: Yes
		/// </summary>
		private long _finished;
		public bool IsFinished => Interlocked.Read(ref _finished) == 1;

		private CancellationTokenSource Stop { get; }

		public CcjRoundTimeouter(CcjRound round, CcjRoundPhase phaseToTimeout, TimeSpan timeout)
		{
			_finished = 0;
			Round = Guard.NotNull(nameof(round), round);
			PhaseToTimeout = phaseToTimeout;
			Timeout = timeout;
		}

		public void Start()
		{
			Task.Run(async () =>
			{
				try
				{
					await Task.Delay(Timeout, Stop.Token);
					// If timed out:

					// If different phase, then there's nothing to timeout.
					if(PhaseToTimeout != Round.Phase)
					{
						return;
					}
					// If not running, then there's nothing to timeout.
					if (Round.Status != CcjRoundStatus.Running)
					{
						return;
					}

					if(PhaseToTimeout == CcjRoundPhase.InputRegistration)
					{
						// fail rund + make sure coordinator manages the list properly
					}
					else if(PhaseToTimeout == CcjRoundPhase.ConnectionConfirmation)
					{

					}
					else if (PhaseToTimeout == CcjRoundPhase.ConnectionConfirmation)
					{
						// TODO: Ban alices
					}
					else if (PhaseToTimeout == CcjRoundPhase.ConnectionConfirmation)
					{
						// TODO: Ban alices
					}
					else if (PhaseToTimeout == CcjRoundPhase.Signing)
					{
						// TODO: Ban alices
					}

					return;
				}
				catch (TaskCanceledException ex)
				{
					// If canceled externally:
					Logger.LogTrace<CcjRoundTimeouter>(ex);
				}
				catch (Exception ex)
				{
					Logger.LogDebug<CcjRoundTimeouter>(ex);
				}
				finally
				{
					Stop?.Dispose();
					Interlocked.Exchange(ref _finished, 1);
				}
			});
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (!IsFinished)
					{
						Stop?.Cancel();
						Stop?.Dispose(); // This is disposed in the Start, but just to be sure.
					}
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
