using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.CoinJoin.Client.Rounds
{
	public class ClientState
	{
		public ClientState()
		{
			StateLock = new object();
			WaitingList = new Dictionary<SmartCoin, DateTimeOffset>();
			Rounds = new List<ClientRound>();
		}

		private object StateLock { get; }

		/// <summary>
		/// The coin that is waiting to be mixed. DateTimeOffset: utc, at what time it is allowed to start registering this coin.
		/// </summary>
		private Dictionary<SmartCoin, DateTimeOffset> WaitingList { get; }

		private List<ClientRound> Rounds { get; }

		public bool IsInErrorState { get; private set; }

		public void AddCoinToWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				if (!(WaitingList.ContainsKey(coin) || Rounds.Any(x => x.CoinsRegistered.Contains(coin))))
				{
					WaitingList.Add(coin, DateTimeOffset.UtcNow);
					Logger.LogInfo($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
				}
			}
		}

		public IEnumerable<OutPoint> GetSpentCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Where(x => x.IsSpent()).Select(x => x.OutPoint).ToArray();
			}
		}

		public void RemoveCoinFromWaitingList(SmartCoin coin)
		{
			lock (StateLock)
			{
				if (WaitingList.ContainsKey(coin))
				{
					WaitingList.Remove(coin);
					Logger.LogInfo($"Coin removed from the waiting list: {coin.Index}:{coin.TransactionId}.");
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

		public bool Contains(IEnumerable<OutPoint> outpoints)
		{
			lock (StateLock)
			{
				foreach (OutPoint txo in outpoints)
				{
					if (WaitingList.Keys
						.Concat(Rounds.SelectMany(x => x.CoinsRegistered))
						.Any(x => x.OutPoint == txo))
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

		public SmartCoin GetSingleOrDefaultCoin(OutPoint coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).SingleOrDefault(x => x.OutPoint == coinReference);
			}
		}

		public int GetWaitingListCount()
		{
			lock (StateLock)
			{
				return WaitingList.Count;
			}
		}

		public SmartCoin GetSingleOrDefaultFromWaitingList(OutPoint coinReference)
		{
			lock (StateLock)
			{
				return WaitingList.Keys.SingleOrDefault(x => x.OutPoint == coinReference);
			}
		}

		public IEnumerable<OutPoint> GetAllQueuedCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Concat(Rounds.SelectMany(x => x.CoinsRegistered)).Select(x => x.OutPoint).ToArray();
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

		public IEnumerable<OutPoint> GetAllWaitingCoins()
		{
			lock (StateLock)
			{
				return WaitingList.Keys.Select(x => x.OutPoint).ToArray();
			}
		}

		public IEnumerable<OutPoint> GetAllRegisteredCoins()
		{
			lock (StateLock)
			{
				return Rounds.SelectMany(x => x.CoinsRegistered).Select(x => x.OutPoint).ToArray();
			}
		}

		public IEnumerable<OutPoint> GetRegistrableCoins(int maximumInputCountPerPeer, Money denomination, Money feePerInputs, Money feePerOutputs)
		{
			lock (StateLock)
			{
				if (!WaitingList.Any()) // To avoid computations.
				{
					return Enumerable.Empty<OutPoint>();
				}

				Money amountNeededExceptInputFees = denomination + (feePerOutputs * 2);

				var coins = WaitingList
					.Where(x => x.Value <= DateTimeOffset.UtcNow)
					.Select(x => x.Key) // Only if registering coins is already allowed.
					.Where(x => x.Confirmed)
					.ToList(); // So to not redo it in every cycle.

				bool lazyMode = false;

				for (int i = 1; i <= maximumInputCountPerPeer; i++) // The smallest number of coins we can register the better it is.
				{
					List<IEnumerable<SmartCoin>> coinGroups;
					Money amountNeeded = amountNeededExceptInputFees + (feePerInputs * i); // If the sum reaches the minimum amount.

					if (lazyMode) // Do the largest valid combination.
					{
						IEnumerable<SmartCoin> highestValueEnumeration = coins.OrderByDescending(x => x.Amount).Take(i);
						coinGroups = highestValueEnumeration.Sum(x => x.Amount) >= amountNeeded
							? new List<IEnumerable<SmartCoin>> { highestValueEnumeration }
							: new List<IEnumerable<SmartCoin>>();
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
						coinGroups = coinGroups.OrderBy(x => x.Sum(y => y.HdPubKey.AnonymitySet)).ToList();
					}
					else // Else coin merging will happen.
					{
						// Prefer the lowest amount sum, so perfect mix should be more likely.
						coinGroups = coinGroups.OrderBy(x => x.Sum(y => y.Amount)).ToList();

						// Try to register the largest anonymity set, so red and green coins input merging should be less likely.
						coinGroups = coinGroups.OrderByDescending(x => x.Sum(y => y.HdPubKey.AnonymitySet)).ToList();
					}

					coinGroups = coinGroups.OrderBy(x => x.Count(y => y.Confirmed == false)).ToList(); // Where the lowest amount of unconfirmed coins there are.

					var best = coinGroups.FirstOrDefault();

					if (best is { })
					{
						var bestSet = best.ToHashSet();

						// -- OPPORTUNISTIC CONSOLIDATION --
						// https://github.com/zkSNACKs/WalletWasabi/issues/1651
						if (bestSet.Count < maximumInputCountPerPeer) // Ensure limits.
						{
							// Generating toxic change leads to mass merging so it's better to merge sooner in coinjoin than the user do it himself in a non-CJ.
							// The best selection's anonset should not be lowered by this merge.
							int bestMinAnonset = bestSet.Min(x => x.HdPubKey.AnonymitySet);
							var bestSum = Money.Satoshis(bestSet.Sum(x => x.Amount));

							if (!bestSum.Almost(amountNeeded, Money.Coins(0.0001m)) // Otherwise it wouldn't generate change so consolidation would make no sense.
								&& bestMinAnonset > 1) // Red coins should never be merged.
							{
								IEnumerable<SmartCoin> coinsThatCanBeConsolidated = coins
									.Except(bestSet) // Get all the registrable coins, except the already chosen ones.
									.Where(x =>
										x.HdPubKey.AnonymitySet >= bestMinAnonset // The anonset must be at least equal to the bestSet's anonset so we do not ruin the change's after mix anonset.
										&& x.HdPubKey.AnonymitySet > 1 // Red coins should never be merged.
										&& x.Amount < amountNeeded // The amount needs to be smaller than the amountNeeded (so to make sure this is toxic change.)
										&& bestSum + x.Amount > amountNeeded) // Sanity check that the amount added do not ruin the registration.
									.OrderBy(x => x.Amount); // Choose the smallest ones.

								if (coinsThatCanBeConsolidated.Count() > 1) // Because the last one change should not be circulating, ruining privacy.
								{
									var bestCoinToAdd = coinsThatCanBeConsolidated.First();
									bestSet.Add(bestCoinToAdd);
								}
							}
						}

						return bestSet.Select(x => x.OutPoint).ToArray();
					}
				}

				return Enumerable.Empty<OutPoint>(); // Inputs are too small, max input to be registered is reached.
			}
		}

		public bool AnyCoinsQueued()
		{
			lock (StateLock)
			{
				return WaitingList.Any() || Rounds.SelectMany(x => x.CoinsRegistered).Any();
			}
		}

		public IEnumerable<ClientRound> GetActivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.Registration is { } && x.State.Phase >= RoundPhase.ConnectionConfirmation).ToArray();
			}
		}

		public IEnumerable<ClientRound> GetPassivelyMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.Registration is { } && x.State.Phase == RoundPhase.InputRegistration).ToArray();
			}
		}

		public IEnumerable<ClientRound> GetAllMixingRounds()
		{
			lock (StateLock)
			{
				return Rounds.Where(x => x.Registration is { }).ToArray();
			}
		}

		public ClientRound GetRegistrableRoundOrDefault()
		{
			lock (StateLock)
			{
				return Rounds.FirstOrDefault(x => x.State.Phase == RoundPhase.InputRegistration);
			}
		}

		public ClientRound GetLatestRoundOrDefault()
		{
			lock (StateLock)
			{
				return Rounds.LastOrDefault();
			}
		}

		public ClientRound GetMostAdvancedRoundOrDefault()
		{
			lock (StateLock)
			{
				var foundAdvanced = Rounds.FirstOrDefault(x => x.State.Phase != RoundPhase.InputRegistration);
				if (foundAdvanced is { })
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

		public ClientRound GetSingleOrDefaultRound(long roundId)
		{
			lock (StateLock)
			{
				return Rounds.SingleOrDefault(x => x.State.RoundId == roundId);
			}
		}

		public void UpdateRoundsByStates(ConcurrentDictionary<OutPoint, IEnumerable<HdPubKeyBlindedPair>> exposedLinks, params RoundStateResponseBase[] allRunningRoundsStates)
		{
			Guard.NotNullOrEmpty(nameof(allRunningRoundsStates), allRunningRoundsStates);
			IsInErrorState = false;
			lock (StateLock)
			{
				// Find the rounds that are not running anymore
				// Put their coins back to the waiting list
				// Remove them
				// Find the rounds that need to be updated
				// Update them

				IEnumerable<long> roundsToRemove = Rounds.Select(x => x.State.RoundId).Where(y => !allRunningRoundsStates.Select(z => z.RoundId).Contains(y));

				foreach (ClientRound round in Rounds.Where(x => roundsToRemove.Contains(x.State.RoundId)))
				{
					var newSuccessfulRoundCount = allRunningRoundsStates.FirstOrDefault()?.SuccessfulRoundCount;
					bool roundFailed = newSuccessfulRoundCount is { } && round.State.SuccessfulRoundCount == newSuccessfulRoundCount;
					if (roundFailed)
					{
						IsInErrorState = true;
					}

					foreach (SmartCoin coin in round.CoinsRegistered)
					{
						if (round.Registration.IsPhaseActionsComleted(RoundPhase.Signing))
						{
							var delayRegistration = TimeSpan.FromSeconds(60);
							WaitingList.Add(coin, DateTimeOffset.UtcNow + delayRegistration);
							Logger.LogInfo($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}, but its registration is not allowed till {delayRegistration.TotalSeconds} seconds, because this coin might already be spent.");

							if (roundFailed)
							{
								// Cleanup non-exposed links.
								foreach (OutPoint input in round.Registration.CoinsRegistered.Select(x => x.OutPoint))
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
							Logger.LogInfo($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
						}
					}

					Logger.LogInfo($"Round ({round.State.RoundId}) removed. Reason: It's not running anymore.");
				}
				Rounds.RemoveAll(x => roundsToRemove.Contains(x.State.RoundId));

				foreach (ClientRound round in Rounds)
				{
					if (allRunningRoundsStates.Select(x => x.RoundId).Contains(round.State.RoundId))
					{
						round.State = allRunningRoundsStates.Single(x => x.RoundId == round.State.RoundId);
					}
				}

				foreach (RoundStateResponseBase state in allRunningRoundsStates)
				{
					if (!Rounds.Select(x => x.State.RoundId).Contains(state.RoundId))
					{
						var r = new ClientRound(state);
						Rounds.Add(r);
						Logger.LogInfo($"Round ({r.State.RoundId}) added.");
					}
				}
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
						Logger.LogInfo($"Coin added to the waiting list: {coin.Index}:{coin.TransactionId}.");
					}
					round.ClearRegistration();
					Logger.LogInfo($"Round ({round.State.RoundId}) registration is cleared.");
				}
			}
		}
	}
}
