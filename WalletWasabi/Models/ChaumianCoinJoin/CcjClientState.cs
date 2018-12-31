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
				return WaitingList.Count + Rounds.Sum(x => x.CoinsRegistered.Count());
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
				if (!WaitingList.Any()) // To avoid computations.
				{
					return Enumerable.Empty<(uint256 txid, uint index)>();
				}

				Money amountNeededExceptInputFees = denomination + (feePerOutputs * 2);
				var confirmedResult = GetRegistrableCoinsNoLock(maximumInputCountPerPeer, feePerInputs, amountNeededExceptInputFees, allowUnconfirmedZeroLink: false);
				if (confirmedResult.Any())
				{
					return confirmedResult;
				}
				else
				{
					return GetRegistrableCoinsNoLock(maximumInputCountPerPeer, feePerInputs, amountNeededExceptInputFees, allowUnconfirmedZeroLink: true);
				}
			}
		}

		private IEnumerable<(uint256 txid, uint index)> GetRegistrableCoinsNoLock(int maximumInputCountPerPeer, Money feePerInputs, Money amountNeededExceptInputFees, bool allowUnconfirmedZeroLink)
		{
			if (!WaitingList.Any()) // To avoid computations.
			{
				return Enumerable.Empty<(uint256 txid, uint index)>();
			}

			Func<SmartCoin, bool> confirmationPredicate;
			if (allowUnconfirmedZeroLink)
			{
				confirmationPredicate = x => x.Confirmed || x.Label.StartsWith("ZeroLink", StringComparison.Ordinal);
			}
			else
			{
				confirmationPredicate = x => x.Confirmed;
			}

			var coins = WaitingList
				.Where(x => x.Value <= DateTimeOffset.UtcNow)
				.Select(x => x.Key) // Only if registering coins is already allowed.
				.Where(confirmationPredicate);

			for (int i = 1; i <= maximumInputCountPerPeer; i++) // The smallest number of coins we can register the better it is.
			{
				IEnumerable<SmartCoin> best = coins.GetPermutations(i)
					.Where(x => x.Sum(y => y.Amount) >= amountNeededExceptInputFees + (feePerInputs * i)) // If the sum reaches the minimum amount.
					.OrderBy(x => x.Count(y => y.Confirmed == false)) // Where the lowest amount of unconfirmed coins there are.
					.ThenBy(x => x.Sum(y => y.AnonymitySet)) // First try t register with the smallest anonymity set.
					.ThenBy(x => x.Sum(y => y.Amount)) // Then the lowest amount, so perfect mix should be more likely.
					.FirstOrDefault();
				if (best != default)
				{
					return best.Select(x => (x.TransactionId, x.Index)).ToArray();
				}
			}

			return Enumerable.Empty<(uint256 txid, uint index)>(); // Inputs are too small, max input to be registered is reached.
		}

		public IEnumerable<long> GetActivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => !(x.Registration is null) && x.State.Phase >= CcjRoundPhase.ConnectionConfirmation).Select(x => x.State.RoundId).ToArray();
			}
		}

		public IEnumerable<long> GetPassivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => !(x.Registration is null) && x.State.Phase == CcjRoundPhase.InputRegistration).Select(x => x.State.RoundId).ToArray();
			}
		}

		public IEnumerable<long> GetAllMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => !(x.Registration is null)).Select(x => x.State.RoundId).ToArray();
			}
		}

		public CcjClientRound GetRegistrableRoundOrDefault()
		{
			lock (StateLock)
			{
				return Rounds.FirstOrDefault(x => x.State.Phase == CcjRoundPhase.InputRegistration);
			}
		}

		public CcjClientRound GetLatestRoundOrDefault()
		{
			lock (StateLock)
			{
				return Rounds.LastOrDefault();
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
			IsInErrorState = false;
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
						if (round.Registration.IsPhaseActionsComleted(CcjRoundPhase.Signing))
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

					round?.Registration?.AliceClient?.Dispose();

					var newSuccessfulRoundCount = allRunningRoundsStates.FirstOrDefault()?.SuccessfulRoundCount;
					if (newSuccessfulRoundCount != null && round.State.SuccessfulRoundCount == newSuccessfulRoundCount)
					{
						IsInErrorState = true;
					}

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

		public bool IsInErrorState { get; private set; }

		public void AddOrReplaceRound(CcjClientRound round)
		{
			lock (StateLock)
			{
				foreach (var r in Rounds.Where(x => x.State.RoundId == round.State.RoundId))
				{
					r?.Registration?.AliceClient?.Dispose();
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
				foreach (var aliceClient in Rounds?.Select(x => x?.Registration?.AliceClient))
				{
					aliceClient?.Dispose();
				}
			}
		}
	}
}
