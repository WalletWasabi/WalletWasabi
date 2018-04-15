using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.ChaumianCoinJoin
{
	public class CcjRound
	{
		public CcjRoundPhase Phase { get; private set; }
		private static AsyncLock PhaseExecutionLock { get; } = new AsyncLock();

		public CcjRoundStatus Status { get; private set; }

		public CcjRound()
		{
			Phase = CcjRoundPhase.InputRegistration;
			Status = CcjRoundStatus.NotStarted;
		}

		public async Task ExecuteNextPhaseAsync()
		{
			using (await PhaseExecutionLock.LockAsync())
			{
				try
				{
					if (Status == CcjRoundStatus.NotStarted)
					{
						Status = CcjRoundStatus.Running;
					}
					else if (Phase == CcjRoundPhase.InputRegistration)
					{
						Phase = CcjRoundPhase.ConnectionConfirmation;
					}
					else if (Phase == CcjRoundPhase.ConnectionConfirmation)
					{
						Phase = CcjRoundPhase.OutputRegistration;
					}
					else if (Phase == CcjRoundPhase.OutputRegistration)
					{
						Phase = CcjRoundPhase.Signing;
					}
					else
					{
						throw new InvalidOperationException("Last phase is reached.");
					}
				}
				catch (Exception ex)
				{
					Logger.LogError<CcjRound>(ex);
					Status = CcjRoundStatus.Failed;
					throw;
				}
			}
		}
	}
}
