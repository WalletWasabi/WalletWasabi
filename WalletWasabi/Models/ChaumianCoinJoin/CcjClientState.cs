using NBitcoin;
using System;
using System.Collections.Concurrent;
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

		public IEnumerable<TxoRef> GetSpentCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Where(x => !x.Unspent).Select(x => x.GetTxoRef()).ToArray();
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

		public bool Contains(params TxoRef[] txos)
		{
			lock (StateLock)
			{
				foreach (TxoRef txo in txos)
				{
					if (WaitingList.Keys
						.Concat(Rounds.SelectMany(x => x.CoinsRegistered))
						.Any(x => x.GetTxoRef() == txo))
					{
						return true;
					}
				}
			}

			return false;
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.SingleOrDefault(x => x == coin);
			}
		}

		public SmartCoin GetSingleOrDefaultCoin(TxoRef coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).SingleOrDefault(x => x.GetTxoRef() == coinReference);
			}
		}

		public int GetWaitingListCount()
		{
			lock (StateLock)
			{
				return WaitingList.Count;
			}
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList(TxoRef coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.SingleOrDefault(x => x.GetTxoRef() == coinReference);
			}
		}

		public IEnumerable<TxoRef> GetAllQueuedCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Select(x => x.GetTxoRef()).ToArray();
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

		public IEnumerable<TxoRef> GetAllWaitingCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Select(x => x.GetTxoRef()).ToArray();
			}
		}

		public IEnumerable<TxoRef> GetAllRegisteredCoins()
		{
			lock (StateLock)
			{
				return Rounds.SelectMany(x => x.CoinsRegistered).Select(x => x.GetTxoRef()).ToArray();
			}
		}

		public IEnumerable<TxoRef> GetRegistrableCoins(int maximumInputCountPerPeer, Money denomination, Money feePerInputs, Money feePerOutputs)
		{
			lock (StateLock)
			{
				if (!WaitingList.Any()) // To avoid computations.
				{
					return Enumerable.Empty<TxoRef>();
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

		private IEnumerable<TxoRef> GetRegistrableCoinsNoLock(int maximumInputCountPerPeer, Money feePerInputs, Money amountNeededExceptInputFees, bool allowUnconfirmedZeroLink)
		{
			if (!WaitingList.Any()) // To avoid computations.
			{
				return Enumerable.Empty<TxoRef>();
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
				.Where(confirmationPredicate)
				.ToList(); // So to not redo it in every cycle.

			bool lazyMode = false;

			for (int i = 1; i <= maximumInputCountPerPeer; i++) // The smallest number of coins we can register the better it is.
			{
				List<IEnumerable<SmartCoin>> coinGroups;
				Money amountNeeded = amountNeededExceptInputFees + (feePerInputs * i); // If the sum reaches the minimum amount.

				if (lazyMode) // Do the largest valid combination.
				{
					IEnumerable<SmartCoin> highestValueEnumeration = coins.OrderByDescending(x => x.Amount).Take(i);
					if (highestValueEnumeration.Sum(x => x.Amount) >= amountNeeded)
					{
						coinGroups = new List<IEnumerable<SmartCoin>> { highestValueEnumeration };
					}
					else
					{
						coinGroups = new List<IEnumerable<SmartCoin>>();
					}
				}
				else
				{
					DateTimeOffset start = DateTimeOffset.UtcNow;

					coinGroups = coins.GetPermutations(i, amountNeeded).ToList();

					if (DateTimeOffset.UtcNow - start > TimeSpan.FromMilliseconds(10)) // If the permutations took long then then if there's a nextTime, calculating permutations would be too CPU intensive.
					{
						lazyMode = true;
					}
				}

				if (i == 1) // If only one coin is to be registered.
				{
					// Prefer the largest one, so more mixing volume is more likely.
					coinGroups = coinGroups.OrderByDescending(x => x.Sum(y => y.Amount)).ToList();

					// Try to register with the smallest anonymity set, so new unmixed coins come to the mix.
					coinGroups = coinGroups.OrderBy(x => x.Sum(y => y.AnonymitySet)).ToList();
				}
				else // Else coin merging will happen.
				{
					// Prefer the lowest amount sum, so perfect mix should be more likely.
					coinGroups = coinGroups.OrderBy(x => x.Sum(y => y.Amount)).ToList();

					// Try to register the largest anonymity set, so red and green coins input merging should be less likely.
					coinGroups = coinGroups.OrderByDescending(x => x.Sum(y => y.AnonymitySet)).ToList();
				}

				coinGroups = coinGroups.OrderBy(x => x.Count(y => y.Confirmed == false)).ToList(); // Where the lowest amount of unconfirmed coins there are.

				IEnumerable<SmartCoin> best = coinGroups.FirstOrDefault();

				if (best != default)
				{
					var bestSet = best.ToHashSet();

					// -- OPPORTUNISTIC CONSOLIDATION --
					// https://github.com/zkSNACKs/WalletWasabi/issues/1651
					if (bestSet.Count < maximumInputCountPerPeer) // Ensure limits.
					{
						// Generating toxic change leads to mass merging so it's better to merge sooner in coinjoin than the user do it himself in a non-CJ.
						// The best selection's anonset should not be lowered by this merge.
						int bestMinAnonset = bestSet.Min(x => x.AnonymitySet);
						var bestSum = Money.Satoshis(bestSet.Sum(x => x.Amount));

						if (!bestSum.Almost(amountNeeded, Money.Coins(0.0001m)) // Otherwise it wouldn't generate change so consolidation would make no sense.
							&& bestMinAnonset > 1) // Red coins should never be merged.
						{
							IEnumerable<SmartCoin> coinsThoseCanBeConsolidated = coins
								.Except(bestSet) // Get all the registrable coins, except the already chosen ones.
								.Where(x =>
									x.AnonymitySet >= bestMinAnonset // The anonset must be at least equal to the bestSet's anonset so we don't ruin the change's after mix anonset.
									&& x.AnonymitySet > 1 // Red coins should never be merged.
									&& x.Amount < amountNeeded // The amount need to be smaller than the amountNeeded (so to make sure this is toxic change.)
									&& (bestSum + x.Amount) > amountNeeded) // Sanity check that the amount added don't ruin the registration.
								.OrderBy(x => x.Amount); // Choose the smallest ones.

							if (coinsThoseCanBeConsolidated.Count() > 1) // Because the last one change should not be circulating, ruining privacy.
							{
								var bestCoinToAdd = coinsThoseCanBeConsolidated.First();
								bestSet.Add(bestCoinToAdd);
							}
						}
					}

					return bestSet.Select(x => x.GetTxoRef()).ToArray();
				}
			}

			return Enumerable.Empty<TxoRef>(); // Inputs are too small, max input to be registered is reached.
		}

		public bool AnyCoinsQueued()
		{
			lock (StateLock)
			{
				if (WaitingList.Any())
				{
					return true;
				}

				foreach (var coins in Rounds.Select(x => x.CoinsRegistered))
				{
					if (coins.Any())
					{
						return true;
					}
				}

				return false;
			}
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

		public void UpdateRoundsByStates(ConcurrentDictionary<TxoRef, IEnumerable<HdPubKeyBlindedPair>> exposedLinks, params CcjRunningRoundState[] allRunningRoundsStates)
		{
			Guard.NotNullOrEmpty(nameof(allRunningRoundsStates), allRunningRoundsStates);
			IsInErrorState = false;
			lock (StateLock)
			{
				// Find the rounds those are not running anymore
				//	Put their coins back to the waiting list
				//	Remove them
				// Find the rounds those needs to be updated
				//	Update them

				IEnumerable<long> roundsToRemove = Rounds.Select(x => x.State.RoundId).Where(y => !allRunningRoundsStates.Select(z => z.RoundId).Contains(y));

				foreach (CcjClientRound round in Rounds.Where(x => roundsToRemove.Contains(x.State.RoundId)))
				{
					var newSuccessfulRoundCount = allRunningRoundsStates.FirstOrDefault()?.SuccessfulRoundCount;
					bool roundFailed = newSuccessfulRoundCount != null && round.State.SuccessfulRoundCount == newSuccessfulRoundCount;
					if (roundFailed)
					{
						IsInErrorState = true;
					}

					foreach (SmartCoin coin in round.CoinsRegistered)
					{
						if (round.Registration.IsPhaseActionsComleted(CcjRoundPhase.Signing))
						{
							var delayRegistration = TimeSpan.FromSeconds(60);
							WaitingList.Add(coin, DateTimeOffset.UtcNow + delayRegistration);
							Logger.LogInfo<CcjClientState>($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}, but its registration is not allowed till {delayRegistration.TotalSeconds} seconds, because this coin might already be spent.");

							if (roundFailed)
							{
								// Cleanup non-exposed links.
								foreach (TxoRef input in round.Registration.CoinsRegistered.Select(x => x.GetTxoRef()))
								{
									if (exposedLinks.ContainsKey(input)) // This should always be the case.
									{
										exposedLinks[input] = exposedLinks[input].Where(x => !x.IsBlinded);
									}
								}
							}
						}
						else
						{
							WaitingList.Add(coin, DateTimeOffset.UtcNow);
							Logger.LogInfo<CcjClientState>($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
						}
					}

					round?.Registration?.AliceClient?.Dispose();

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
