using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class CcjCoordinator
	{
		private List<CcjRound> Rounds { get; }
		private AsyncLock RoundsListLock { get; }

		public CcjCoordinator()
		{
			Rounds = new List<CcjRound>();
			RoundsListLock = new AsyncLock();
		}

		public async Task StartNewRoundAsync()
		{
			using (RoundsListLock.Lock())
			{
				if (Rounds.Count(x => x.Status == CcjRoundStatus.Running) > 1)
				{
					throw new InvalidOperationException("Maximum two concurrently running round is allowed the same time.");
				}
				var round = new CcjRound();
				await round.ExecuteNextPhaseAsync();
				Rounds.Add(round);
			}
		}

		public CcjRound GetLastSuccessfulRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.LastOrDefault(x => x.Status == CcjRoundStatus.Succeded);
			}
		}

		public CcjRound GetLastFailedRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.LastOrDefault(x => x.Status == CcjRoundStatus.Failed);
			}
		}

		public CcjRound GetLastRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.LastOrDefault(x => x.Status != CcjRoundStatus.Running);
			}
		}

		public CcjRound GetCurrentRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.First(x => x.Status == CcjRoundStatus.Running); // not FirstOrDefault, it must always exist
			}
		}

		public CcjRound GetNextRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.LastOrDefault(x => x.Status == CcjRoundStatus.Running);
			}
		}
	}
}
