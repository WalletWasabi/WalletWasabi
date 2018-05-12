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

		public IEnumerable<(uint256 txid, int index)> GetSpentCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Where(x => !x.Unspent).Select(x => (x.TransactionId, x.Index)).ToArray();
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

		public SmartCoin GetSingleOrDefaultCoin((uint256 txid, int index) coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).SingleOrDefault(x => x.TransactionId == coinReference.txid && x.Index == coinReference.index);
			}
		}

		public int GetWaitingListCount()
		{
			lock (StateLock)
			{
				return WaitingList.Count;
			}
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList((uint256 txid, int index) coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.SingleOrDefault(x => x.TransactionId == coinReference.txid && x.Index == coinReference.index);
			}
		}

		public IEnumerable<(uint256 txid, int index)> GetAllCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Select(x=>(x.TransactionId, x.Index)).ToArray();
			}
		}

		public IEnumerable<(uint256 txid, int index)> GetRegistrableCoins(int maximumInputCountPerPeer, Money denomination, Money feePerInputs, Money feePerOutputs)
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
						return Enumerable.Empty<(uint256 txid, int index)>(); // Inputs are too small, max input to be registered is reached.
					}

					amountSoFar += coin.Amount;
					if (amountSoFar > amountNeededExceptInputFees + feePerInputs * coinsToRegister.Count)
					{
						// If input count doesn't reach the max input registration AND there are enough coins queued, then can register to mix.
						return coinsToRegister.Select(x => (x.TransactionId, x.Index)).ToArray();
					}
				}

				return Enumerable.Empty<(uint256 txid, int index)>(); // Amount is never reached.
			}
		}

		public IEnumerable<long> GetActivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.AliceClient != null && x.State.Phase >= CcjRoundPhase.ConnectionConfirmation).Select(x=>x.State.RoundId).ToArray();
			}
		}

		public IEnumerable<long> GetPassivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.AliceClient != null && x.State.Phase == CcjRoundPhase.InputRegistration).Select(x => x.State.RoundId).ToArray();
			}
		}

		public int GetSmallestRegistrationTimeout()
		{
			lock (StateLock)
			{
				if (Rounds.Count == 0)
				{
					return 0;
				}
				else
				{
					return Rounds.Min(x => x.State.RegistrationTimeout);
				}
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
				foreach(var round in Rounds.Where(x=> roundsToRemove.Contains(x.State.RoundId)))
				{
					round.AliceClient?.Dispose();
				}
				Rounds.RemoveAll(x => roundsToRemove.Contains(x.State.RoundId));

				foreach (var round in Rounds)
				{
					if (allRunningRoundsStates.Select(x => x.RoundId).Contains(round.State.RoundId))
					{
						round.State = allRunningRoundsStates.Single(x => x.RoundId == round.State.RoundId);
					}
				}

				foreach(var state in allRunningRoundsStates)
				{
					if(!Rounds.Select(x=>x.State.RoundId).Contains(state.RoundId))
					{
						var r = new CcjClientRound(state);
						Rounds.Add(r);
					}
				}
			}
		}

		public CcjClientRound GetRegistrableRoundOrDefault()
		{
			lock(StateLock)
			{
				return Rounds.FirstOrDefault(x => x.State.Phase == CcjRoundPhase.InputRegistration);
			}
		}
		
		public void AddOrReplaceRound(CcjClientRound round)
		{
			lock (StateLock)
			{
				foreach (var r in Rounds.Where(x => x.State.RoundId == round.State.RoundId))
				{
					r.AliceClient?.Dispose();
				}
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

		public void DisposeAllAliceClients()
		{
			lock (StateLock)
			{
				foreach (var aliceClient in Rounds.Select(x => x.AliceClient))
				{
					aliceClient?.Dispose();
				}
			}
		}
	}
}
