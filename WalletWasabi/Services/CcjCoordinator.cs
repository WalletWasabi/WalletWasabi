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
	public class CcjCoordinator
	{
		private List<CcjRound> Rounds { get; }
		private AsyncLock RoundsListLock { get; }

		public RPCClient RpcClient { get; }

		public CcjCoordinator(RPCClient rpc)
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);

			Rounds = new List<CcjRound>();
			RoundsListLock = new AsyncLock();
		}

		public async Task StartNewRoundAsync(CcjRoundConfig roundConfig)
		{
			using (await RoundsListLock.LockAsync())
			{
				if (Rounds.Count(x => x.Status == CcjRoundStatus.Running) > 1)
				{
					throw new InvalidOperationException("Maximum two concurrently running round is allowed the same time.");
				}
				
				var round = new CcjRound(RpcClient, roundConfig);
				await round.ExecuteNextPhaseAsync(TimeSpan.FromSeconds((long)roundConfig.InputRegistrationTimeout), async () => 
				{
					// This will happen outside the lock.

					// Only fail if less two one Alice is registered.
					// Don't ban anyone, it's ok if they lost connection.
					int aliceCountAfterInputRegistrationTimeout = round.CountAlices();
					if (aliceCountAfterInputRegistrationTimeout < 2)
					{
						round.Fail();
						await StartNewRoundAsync(roundConfig);
					}
					else
					{
						round.UpdateAnonymitySet(aliceCountAfterInputRegistrationTimeout);
						// Progress to the next phase, which will be ConnectionConfirmation
						await round.ExecuteNextPhaseAsync(TimeSpan.FromSeconds((long)roundConfig.ConnectionConfirmationTimeout), async () =>
						{
							// Only fail if less two one Alice is registered.
							// Don't ban anyone, it's ok if they lost connection.
							round.RemoveAlicesBy(AliceState.InputsRegistered);
							int aliceCountAfterConnectionConfirmationTimeout = round.CountAlices();
							if (aliceCountAfterConnectionConfirmationTimeout < 2)
							{
								round.Fail();
								await StartNewRoundAsync(roundConfig);
							}
							else
							{
								round.UpdateAnonymitySet(aliceCountAfterConnectionConfirmationTimeout);
								// Progress to the next phase, which will be ConnectionConfirmation
								await round.ExecuteNextPhaseAsync(TimeSpan.FromSeconds((long)roundConfig.OutputRegistrationTimeout), async () =>
								{
									// Output registration never fails.
									// We don't know which Alice to ban.
									// Therefore proceed to signing, and whichever Alice doesn't sign ban.
									await round.ExecuteNextPhaseAsync(TimeSpan.FromSeconds((long)roundConfig.SigningTimeout), async () =>
									{
										round.Fail();
										await StartNewRoundAsync(roundConfig);
										// ToDo: Ban Alices those states are not SignedCoinJoin
									});
								});
							}
						});
					}
				});
				Rounds.Add(round);
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
	}
}
