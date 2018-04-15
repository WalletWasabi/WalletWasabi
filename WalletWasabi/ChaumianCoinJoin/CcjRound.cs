using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.ChaumianCoinJoin
{
	public class CcjRound
	{
		public Money Denomination { get; }

		public CcjRoundPhase Phase { get; private set; }
		private static AsyncLock PhaseExecutionLock { get; } = new AsyncLock();

		public CcjRoundStatus Status { get; private set; }

		public CcjRound(Money denomination)
		{
			Denomination = Guard.NotNull(nameof(denomination), denomination);

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
					else if(Status != CcjRoundStatus.Running) // Failed or succeeded, swallow
					{
						return;
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

		public void Fail()
		{
			using (PhaseExecutionLock.Lock())
			{
				Status = CcjRoundStatus.Failed;
			}
		}
	}
}
