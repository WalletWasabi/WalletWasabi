using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class CcjClientState
	{
		private object StateLock { get; }

		private List<SmartCoin> WaitingList { get; }

		private List<CcjClientRound> Rounds { get; }

		public CcjClientState()
		{
			StateLock = new object();
			WaitingList = new List<SmartCoin>();
			Rounds = new List<CcjClientRound>();
		}

		public void AddCoinToWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				if (!(WaitingList.Contains(coin) || Rounds.Any(x => x.CoinsRegistered.Contains(coin))))
				{
					WaitingList.Add(coin);
				}
			}
		}

		public void RemoveSpentCoinsFromWaitingList()
		{
			lock (StateLock)
			{
				WaitingList.RemoveAll(x => !x.Unspent);
			}
		}

		public void RemoveCoinFromWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				if (WaitingList.Contains(coin))
				{
					WaitingList.Remove(coin);
				}
			}
		}

		public bool Contains(SmartCoin coin)
		{
			lock (StateLock)
			{
				return WaitingList.Contains(coin) || Rounds.Any(x => x.CoinsRegistered.Contains(coin));
			}
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				return WaitingList.SingleOrDefault(x => x == coin);
			}
		}

		public IEnumerable<SmartCoin> GetAllCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).ToArray();
			}
		}

		public IEnumerable<SmartCoin> GetRegistrableCoins(int maximumInputCountPerPeer, Money denomination, Money feePerInputs, Money feePerOutputs)
		{
			lock (StateLock)
			{
				var coinsToRegister = new List<SmartCoin>();
				var amountSoFar = Money.Zero;
				Money amountNeededExceptInputFees = denomination + feePerOutputs * 2;
				foreach (SmartCoin coin in WaitingList
								.Where(x => x.Confirmed || x.Label.Contains("CoinJoin", StringComparison.Ordinal)) // Where our label contains CoinJoin, CoinJoins can be registered even if not confirmed, our label will likely be CoinJoin only if it was a previous CoinJoin, otherwise the server will refuse us.
								.OrderByDescending(y => y.Amount) // First order by amount.
								.OrderByDescending(z => z.Confirmed)) // Then order by the amount ordered ienumerable by confirmation, so first try to register confirmed coins.
				{
					coinsToRegister.Add(coin);

					if (maximumInputCountPerPeer < coinsToRegister.Count)
					{
						return Enumerable.Empty<SmartCoin>(); // Inputs are too small, max input to be registered is reached.
					}

					amountSoFar += coin.Amount;
					if (amountSoFar > amountNeededExceptInputFees + feePerInputs * coinsToRegister.Count)
					{
						// If input count doesn't reach the max input registration AND there are enough coins queued, then can register to mix.
						return coinsToRegister;
					}
				}

				return Enumerable.Empty<SmartCoin>(); // Amount is never reached.
			}
		}

		public IEnumerable<CcjClientRound> GetActivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.AliceUniqueId != null && x.State.Phase >= CcjRoundPhase.ConnectionConfirmation).ToArray();
			}
		}

		public IEnumerable<CcjClientRound> GetPassivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.AliceUniqueId != null && x.State.Phase == CcjRoundPhase.InputRegistration).ToArray();
			}
		}

		public int GetSmallestRegistrationTimeout()
		{
			lock (StateLock)
			{
				return Rounds.Min(x => x.State.RegistrationTimeout);
			}
		}

		public CcjClientRound GetSingleOrDefaultRound(long roundId)
		{
			lock (StateLock)
			{
				return Rounds.SingleOrDefault(x => x.State.RoundId == roundId);
			}
		}

		public void UpdateRoundsByStates(params CcjRunningRoundState[] allRunningRoundsStates)
		{
			Guard.NotNullOrEmpty(nameof(allRunningRoundsStates), allRunningRoundsStates);

			lock (StateLock)
			{
				// Find the rounds those aren't running anymore
				//	Put their coins back to the waiting list
				//	Remove them
				// Find the rounds those needs to be updated
				//	Update them

				IEnumerable<long> roundsToRemove = Rounds.Select(x => x.State.RoundId).Where(y => !allRunningRoundsStates.Select(z => z.RoundId).Contains(y));
				foreach (var coin in Rounds.Where(x => roundsToRemove.Contains(x.State.RoundId)).SelectMany(y => y.CoinsRegistered))
				{
					WaitingList.Add(coin);
				}
				Rounds.RemoveAll(x => roundsToRemove.Contains(x.State.RoundId));

				foreach (var round in Rounds)
				{
					if (allRunningRoundsStates.Select(x => x.RoundId).Contains(round.State.RoundId))
					{
						round.State = allRunningRoundsStates.Single(x => x.RoundId == round.State.RoundId);
					}
				}
			}
		}

		public CcjClientRound GetRegistrableRound()
		{
			lock(StateLock)
			{
				return Rounds.First(x => x.State.Phase == CcjRoundPhase.InputRegistration);
			}
		}


		public void AddOrReplaceRound(CcjClientRound round)
		{
			lock (StateLock)
			{
				Rounds.RemoveAll(x => x.State.RoundId == round.State.RoundId);
				Rounds.Add(round);
			}
		}

		public void ClearRoundRegistration(long roundId)
		{
			lock (StateLock)
			{
				foreach (var round in Rounds.Where(x => x.State.RoundId == roundId))
				{
					foreach (var coin in round.CoinsRegistered)
					{
						WaitingList.Add(coin);
					}
					round.ClearRegistration();
				}
			}
		}
	}
}
