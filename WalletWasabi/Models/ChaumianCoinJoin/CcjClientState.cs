using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class CcjClientState
	{
		private object StateLock { get; }

		/// <summary>
		/// The coin that is waiting to be mixed. DateTimeOffset: utc, at what time it is allowed to start registering this coin.
		/// </summary>
		private Dictionary<SmartCoin, DateTimeOffset> WaitingList { get; }

		private List<CcjClientRound> Rounds { get; }

		public CcjClientState()
		{
			StateLock = new object();
			WaitingList = new Dictionary<SmartCoin, DateTimeOffset>();
			Rounds = new List<CcjClientRound>();
		}

		public void UpdateCoin(SmartCoin coin)
		{
			lock (StateLock)
			{
				SmartCoin found = WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).FirstOrDefault(x => x == coin);
				if (found != default)
				{
					if (WaitingList.Keys.Contains(coin))
					{
						coin.CoinJoinInProgress = true;
						WaitingList.Remove(found);
						WaitingList.Add(coin, DateTimeOffset.UtcNow);
						return;
					}

					foreach (CcjClientRound round in Rounds)
					{
						if (round.CoinsRegistered.Contains(coin))
						{
							coin.CoinJoinInProgress = true;
							round.CoinsRegistered.Remove(found);
							round.CoinsRegistered.Add(coin);
							return;
						}
					}
				}
			}
		}

		public void AddCoinToWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				if (!(WaitingList.ContainsKey(coin) || Rounds.Any(x => x.CoinsRegistered.Contains(coin))))
				{
					WaitingList.Add(coin, DateTimeOffset.UtcNow);
					Logger.LogInfo<CcjClientState>($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
				}
			}
		}

		public IEnumerable<(uint256 txid, uint index)> GetSpentCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Where(x => !x.Unspent).Select(x => (x.TransactionId, x.Index)).ToArray();
			}
		}

		public void RemoveCoinFromWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				if (WaitingList.ContainsKey(coin))
				{
					WaitingList.Remove(coin);
					Logger.LogInfo<CcjClientState>($"Coin removed from the waiting list: {coin.Index}:{coin.TransactionId}.");
				}
			}
		}

		public bool Contains(SmartCoin coin)
		{
			lock (StateLock)
			{
				return WaitingList.ContainsKey(coin) || Rounds.Any(x => x.CoinsRegistered.Contains(coin));
			}
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.SingleOrDefault(x => x == coin);
			}
		}

		public SmartCoin GetSingleOrDefaultCoin((uint256 txid, uint index) coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).SingleOrDefault(x => x.TransactionId == coinReference.txid && x.Index == coinReference.index);
			}
		}

		public int GetWaitingListCount()
		{
			lock (StateLock)
			{
				return WaitingList.Count;
			}
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList((uint256 txid, uint index) coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.SingleOrDefault(x => x.TransactionId == coinReference.txid && x.Index == coinReference.index);
			}
		}

		public IEnumerable<(uint256 txid, uint index)> GetAllQueuedCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Select(x => (x.TransactionId, x.Index)).ToArray();
			}
		}

		public Money SumAllQueuedCoinAmounts()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Sum(x => x.Amount);
			}
		}

		public int CountAllQueuedCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Count + Rounds.Sum(x => x.CoinsRegistered.Count);
			}
		}

		public IEnumerable<Money> GetAllQueuedCoinAmounts()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Select(x => x.Amount).ToArray();
			}
		}

		public IEnumerable<(uint256 txid, uint index)> GetAllWaitingCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Select(x => (x.TransactionId, x.Index)).ToArray();
			}
		}

		public IEnumerable<(uint256 txid, uint index)> GetAllRegisteredCoins()
		{
			lock (StateLock)
			{
				return Rounds.SelectMany(x => x.CoinsRegistered).Select(x => (x.TransactionId, x.Index)).ToArray();
			}
		}

		public IEnumerable<(uint256 txid, uint index)> GetRegistrableCoins(int maximumInputCountPerPeer, Money denomination, Money feePerInputs, Money feePerOutputs)
		{
			lock (StateLock)
			{
				var coinsToRegister = new List<SmartCoin>();
				var amountSoFar = Money.Zero;
				Money amountNeededExceptInputFees = denomination + (feePerOutputs * 2);
				foreach (SmartCoin coin in WaitingList
							.Where(y => y.Value <= DateTimeOffset.UtcNow).Select(z => z.Key) // Only if registering coins is already allowed.
								.Where(x => x.Confirmed || x.Label.StartsWith("ZeroLink", StringComparison.Ordinal)) // Where our label contains CoinJoin, CoinJoins can be registered even if not confirmed, our label will likely be CoinJoin only if it was a previous CoinJoin, otherwise the server will refuse us.
								.OrderByDescending(y => y.Amount) // First order by amount.
								.ThenByDescending(z => z.Confirmed)) // Then order by the amount ordered ienumerable by confirmation, so first try to register confirmed coins.
				{
					coinsToRegister.Add(coin);

					if (maximumInputCountPerPeer < coinsToRegister.Count)
					{
						return Enumerable.Empty<(uint256 txid, uint index)>(); // Inputs are too small, max input to be registered is reached.
					}

					amountSoFar += coin.Amount;
					if (amountSoFar > amountNeededExceptInputFees + (feePerInputs * coinsToRegister.Count))
					{
						// If input count doesn't reach the max input registration AND there are enough coins queued, then can register to mix.
						return coinsToRegister.Select(x => (x.TransactionId, x.Index)).ToArray();
					}
				}

				return Enumerable.Empty<(uint256 txid, uint index)>(); // Amount is never reached.
			}
		}

		public IEnumerable<long> GetActivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.AliceClient != null && x.State.Phase >= CcjRoundPhase.ConnectionConfirmation).Select(x => x.State.RoundId).ToArray();
			}
		}

		public IEnumerable<long> GetPassivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.AliceClient != null && x.State.Phase == CcjRoundPhase.InputRegistration).Select(x => x.State.RoundId).ToArray();
			}
		}

		public CcjClientRound GetRegistrableRoundOrDefault()
		{
			lock (StateLock)
			{
				return Rounds.FirstOrDefault(x => x.State.Phase == CcjRoundPhase.InputRegistration);
			}
		}

		public CcjClientRound GetMostAdvancedRoundOrDefault()
		{
			lock (StateLock)
			{
				var foundAdvanced = Rounds.FirstOrDefault(x => x.State.Phase != CcjRoundPhase.InputRegistration);
				if (foundAdvanced != default)
				{
					return foundAdvanced;
				}
				else
				{
					return Rounds.FirstOrDefault();
				}
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

				foreach (CcjClientRound round in Rounds.Where(x => roundsToRemove.Contains(x.State.RoundId)))
				{
					foreach (SmartCoin coin in round.CoinsRegistered)
					{
						if (round.Signed)
						{
							var delayRegistration = TimeSpan.FromSeconds(60);
							WaitingList.Add(coin, DateTimeOffset.UtcNow + delayRegistration);
							Logger.LogInfo<CcjClientState>($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}, but its registration is not allowed till {delayRegistration.TotalSeconds} seconds, because this coin might already be spent.");
						}
						else
						{
							WaitingList.Add(coin, DateTimeOffset.UtcNow);
							Logger.LogInfo<CcjClientState>($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
						}
					}

					round.AliceClient?.Dispose();
					Logger.LogInfo<CcjClientState>($"Round ({round.State.RoundId}) removed. Reason: It's not running anymore.");
				}
				Rounds.RemoveAll(x => roundsToRemove.Contains(x.State.RoundId));

				foreach (CcjClientRound round in Rounds)
				{
					if (allRunningRoundsStates.Select(x => x.RoundId).Contains(round.State.RoundId))
					{
						round.State = allRunningRoundsStates.Single(x => x.RoundId == round.State.RoundId);
					}
				}

				foreach (CcjRunningRoundState state in allRunningRoundsStates)
				{
					if (!Rounds.Select(x => x.State.RoundId).Contains(state.RoundId))
					{
						var r = new CcjClientRound(state);
						Rounds.Add(r);
						Logger.LogInfo<CcjClientState>($"Round ({r.State.RoundId}) added.");
					}
				}
			}
		}

		public void AddOrReplaceRound(CcjClientRound round)
		{
			lock (StateLock)
			{
				foreach (var r in Rounds.Where(x => x.State.RoundId == round.State.RoundId))
				{
					r.AliceClient?.Dispose();
					Logger.LogInfo<CcjClientState>($"Round ({round.State.RoundId}) removed. Reason: It's being replaced.");
				}
				Rounds.RemoveAll(x => x.State.RoundId == round.State.RoundId);
				Rounds.Add(round);
				Logger.LogInfo<CcjClientState>($"Round ({round.State.RoundId}) added.");
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
						WaitingList.Add(coin, DateTimeOffset.UtcNow);
						Logger.LogInfo<CcjClientState>($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
					}
					round.ClearRegistration();
					Logger.LogInfo<CcjClientState>($"Round ({round.State.RoundId}) registration is cleared.");
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
