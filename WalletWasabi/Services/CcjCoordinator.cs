using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class CcjCoordinator : IDisposable
	{
		private List<CcjRound> Rounds { get; }
		private AsyncLock RoundsListLock { get; }

		public RPCClient RpcClient { get; }

		public CcjRoundConfig RoundConfig { get; private set; }

		public CcjCoordinator(RPCClient rpc, CcjRoundConfig roundConfig)
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

			Rounds = new List<CcjRound>();
			RoundsListLock = new AsyncLock();
		}

		public void UpdateRoundConfig(CcjRoundConfig roundConfig)
		{
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);
		}

		public async Task MakeSureTwoRunningRoundsAsync()
		{
			using (await RoundsListLock.LockAsync())
			{
				int runningRoundCount = Rounds.Count(x => x.Status == CcjRoundStatus.Running);
				if (runningRoundCount == 0)
				{
					var round = new CcjRound(RpcClient, RoundConfig);
					round.StatusChanged += Round_StatusChangedAsync;
					await round.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration);
					Rounds.Add(round);

					var round2 = new CcjRound(RpcClient, RoundConfig);
					round2.StatusChanged += Round_StatusChangedAsync;
					await round2.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration);
					Rounds.Add(round2);
				}
				else if(runningRoundCount == 1)
				{
					var round = new CcjRound(RpcClient, RoundConfig);
					round.StatusChanged += Round_StatusChangedAsync;
					await round.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration);
					Rounds.Add(round);
				}
			}
		}

		private async void Round_StatusChangedAsync(object sender, CcjRoundStatus status)
		{
			var round = sender as CcjRound;
			// If finished start a new round.
			if (status == CcjRoundStatus.Failed || status == CcjRoundStatus.Succeded)
			{
				round.StatusChanged -= Round_StatusChangedAsync;
				await MakeSureTwoRunningRoundsAsync();
			}
		}

		public void FailAllRoundsInInputRegistration()
		{
			using (RoundsListLock.Lock())
			{
				foreach (var r in Rounds.Where(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration))
				{
					r.Fail();
				}
			}
		}

		public void FailAllRunningRounds()
		{
			using (RoundsListLock.Lock())
			{
				foreach (var r in Rounds.Where(x => x.Status == CcjRoundStatus.Running))
				{
					r.Fail();
				}
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

		public CcjRound GetCurrentInputRegisterableRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.First(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration); // not FirstOrDefault, it must always exist
			}
		}

		public CcjRound GetNextRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.LastOrDefault(x => x.Status == CcjRoundStatus.Running);
			}
		}

		public bool AnyRunningRoundContainsInput(OutPoint input, out List<Alice> alices)
		{
			using (RoundsListLock.Lock())
			{
				alices = new List<Alice>();
				foreach(var round in Rounds.Where(x=>x.Status == CcjRoundStatus.Running))
				{
					if(round.ContainsInput(input, out List<Alice> roundAlices))
					{
						foreach(var alice in roundAlices)
						{
							alices.Add(alice);
						}
					}
				}
				return alices.Count > 0;
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					using (RoundsListLock.Lock())
					{
						foreach(CcjRound round in Rounds)
						{
							round.StatusChanged -= Round_StatusChangedAsync;
						}
					}
				}

				_disposedValue = true;
			}
		}

		// ~CcjCoordinator() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

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
