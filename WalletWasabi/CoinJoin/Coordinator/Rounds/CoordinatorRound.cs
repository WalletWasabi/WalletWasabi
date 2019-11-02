using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator.Banning;
using WalletWasabi.CoinJoin.Coordinator.MixingLevels;
using WalletWasabi.CoinJoin.Coordinator.Participants;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Coordinator.Rounds
{
	public class CoordinatorRound
	{
		public static long RoundCount;
		public long RoundId { get; }

		public RPCClient RpcClient { get; }
		public Network Network => RpcClient.Network;

		/// <summary>
		/// The confirmation target that will be used and possibly modified before final build.
		/// </summary>
		public int AdjustedConfirmationTarget { get; private set; }

		/// <summary>
		/// The confirmation target that is present in the config file.
		/// </summary>
		public int ConfiguredConfirmationTarget { get; }

		/// <summary>
		/// The rate of confirmation target reduction rate that is present in the config file.
		/// </summary>
		public double ConfiguredConfirmationTargetReductionRate { get; }

		public decimal CoordinatorFeePercent { get; }
		public int AnonymitySet { get; private set; }

		public Money FeePerInputs { get; private set; }
		public Money FeePerOutputs { get; private set; }

		public Transaction UnsignedCoinJoin { get; private set; }
		private string _unsignedCoinJoinHex;

		public MixingLevelCollection MixingLevels { get; }

		public string GetUnsignedCoinJoinHex()
		{
			if (_unsignedCoinJoinHex is null)
			{
				_unsignedCoinJoinHex = UnsignedCoinJoin.ToHex();
			}
			return _unsignedCoinJoinHex;
		}

		public Transaction SignedCoinJoin { get; private set; }

		private List<Alice> Alices { get; }
		private List<Bob> Bobs { get; } // Do not make it a hashset or do not make Bob IEquitable!!!

		private List<UnblindedSignature> RegisteredUnblindedSignatures { get; }
		private object RegisteredUnblindedSignaturesLock { get; }

		private static AsyncLock RoundSynchronizerLock { get; } = new AsyncLock();

		private RoundPhase _phase;
		private object PhaseLock { get; }

		public RoundPhase Phase
		{
			get
			{
				lock (PhaseLock)
				{
					return _phase;
				}
			}

			private set
			{
				var invoke = false;
				lock (PhaseLock)
				{
					if (_phase != value)
					{
						_phase = value;
						invoke = true;
					}
				}
				if (invoke)
				{
					PhaseChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<RoundPhase> PhaseChanged;

		private CoordinatorRoundStatus _status;

		private object StatusLock { get; }

		public CoordinatorRoundStatus Status
		{
			get
			{
				lock (StatusLock)
				{
					return _status;
				}
			}

			private set
			{
				var invoke = false;
				lock (StatusLock)
				{
					if (_status != value)
					{
						_status = value;
						invoke = true;
					}
				}
				if (invoke)
				{
					StatusChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<CoordinatorRoundStatus> StatusChanged;

		public event EventHandler<Transaction> CoinJoinBroadcasted;

		public TimeSpan AliceRegistrationTimeout => ConnectionConfirmationTimeout;

		public TimeSpan InputRegistrationTimeout { get; }
		public DateTimeOffset InputRegistrationTimesout { get; set; }

		public TimeSpan ConnectionConfirmationTimeout { get; }

		public TimeSpan OutputRegistrationTimeout { get; }

		public TimeSpan SigningTimeout { get; }

		public UtxoReferee UtxoReferee { get; }

		public CoordinatorRound(RPCClient rpc, UtxoReferee utxoReferee, CoordinatorRoundConfig config, int adjustedConfirmationTarget, int configuredConfirmationTarget, double configuredConfirmationTargetReductionRate)
		{
			try
			{
				RoundId = Interlocked.Increment(ref RoundCount);

				RpcClient = Guard.NotNull(nameof(rpc), rpc);
				UtxoReferee = Guard.NotNull(nameof(utxoReferee), utxoReferee);
				Guard.NotNull(nameof(config), config);

				AdjustedConfirmationTarget = adjustedConfirmationTarget;
				ConfiguredConfirmationTarget = configuredConfirmationTarget;
				ConfiguredConfirmationTargetReductionRate = configuredConfirmationTargetReductionRate;
				CoordinatorFeePercent = config.CoordinatorFeePercent;
				AnonymitySet = config.AnonymitySet;
				InputRegistrationTimeout = TimeSpan.FromSeconds(config.InputRegistrationTimeout);
				SetInputRegistrationTimesout();
				ConnectionConfirmationTimeout = TimeSpan.FromSeconds(config.ConnectionConfirmationTimeout);
				OutputRegistrationTimeout = TimeSpan.FromSeconds(config.OutputRegistrationTimeout);
				SigningTimeout = TimeSpan.FromSeconds(config.SigningTimeout);

				PhaseLock = new object();
				Phase = RoundPhase.InputRegistration;
				StatusLock = new object();
				Status = CoordinatorRoundStatus.NotStarted;

				RegisteredUnblindedSignatures = new List<UnblindedSignature>();
				RegisteredUnblindedSignaturesLock = new object();

				MixingLevels = new MixingLevelCollection(config.Denomination, new Signer(new Key()));
				for (int i = 0; i < config.MaximumMixingLevelCount - 1; i++)
				{
					MixingLevels.AddNewLevel();
				}

				_unsignedCoinJoinHex = null;

				UnsignedCoinJoin = null;
				SignedCoinJoin = null;

				Alices = new List<Alice>();
				Bobs = new List<Bob>();

				Logger.LogInfo($"New round ({RoundId}) is created.\n\t" +
					$"BaseDenomination: {MixingLevels.GetBaseDenomination().ToString(false, true)} BTC.\n\t" +
					$"{nameof(AdjustedConfirmationTarget)}: {AdjustedConfirmationTarget}.\n\t" +
					$"{nameof(CoordinatorFeePercent)}: {CoordinatorFeePercent}%.\n\t" +
					$"{nameof(AnonymitySet)}: {AnonymitySet}.");
			}
			catch (Exception ex)
			{
				Logger.LogError($"Round ({RoundId}): Could not create.");
				Logger.LogError(ex);
				throw;
			}
		}

		private void SetInputRegistrationTimesout()
		{
			InputRegistrationTimesout = DateTimeOffset.UtcNow + InputRegistrationTimeout;
		}

		public static ConcurrentDictionary<(long roundId, RoundPhase phase), DateTimeOffset> PhaseTimeoutLog { get; } = new ConcurrentDictionary<(long roundId, RoundPhase phase), DateTimeOffset>();

		public async Task ExecuteNextPhaseAsync(RoundPhase expectedPhase, Money feePerInputs = null, Money feePerOutputs = null)
		{
			using (await RoundSynchronizerLock.LockAsync().ConfigureAwait(false))
			{
				try
				{
					Logger.LogInfo($"Round ({RoundId}): Phase change requested: {expectedPhase.ToString()}.");

					if (Status == CoordinatorRoundStatus.NotStarted) // So start the input registration phase
					{
						if (expectedPhase != RoundPhase.InputRegistration)
						{
							return;
						}

						// Calculate fees.
						if (feePerInputs is null || feePerOutputs is null)
						{
							(Money feePerInputs, Money feePerOutputs) fees = await CalculateFeesAsync(RpcClient, AdjustedConfirmationTarget).ConfigureAwait(false);
							FeePerInputs = feePerInputs ?? fees.feePerInputs;
							FeePerOutputs = feePerOutputs ?? fees.feePerOutputs;
						}
						else
						{
							FeePerInputs = feePerInputs;
							FeePerOutputs = feePerOutputs;
						}

						Status = CoordinatorRoundStatus.Running;
					}
					else if (Status != CoordinatorRoundStatus.Running) // Aborted or succeeded, swallow
					{
						return;
					}
					else if (Phase == RoundPhase.InputRegistration)
					{
						if (expectedPhase != RoundPhase.ConnectionConfirmation)
						{
							return;
						}

						Phase = RoundPhase.ConnectionConfirmation;
					}
					else if (Phase == RoundPhase.ConnectionConfirmation)
					{
						if (expectedPhase != RoundPhase.OutputRegistration)
						{
							return;
						}

						Phase = RoundPhase.OutputRegistration;
					}
					else if (Phase == RoundPhase.OutputRegistration)
					{
						if (expectedPhase != RoundPhase.Signing)
						{
							return;
						}

						// Build CoinJoin:

						Money newDenomination = CalculateNewDenomination();
						var transaction = Network.Consensus.ConsensusFactory.CreateTransaction();

						// 2. Add Bob outputs.
						foreach (Bob bob in Bobs.Where(x => x.Level == MixingLevels.GetBaseLevel()))
						{
							transaction.Outputs.AddWithOptimize(newDenomination, bob.ActiveOutputAddress.ScriptPubKey);
						}

						// 2.1 newDenomination may differs from the Denomination at registration, so we may not be able to tinker with
						// additional outputs.
						bool tinkerWithAdditionalMixingLevels = CanUseAdditionalOutputs(newDenomination);

						if (tinkerWithAdditionalMixingLevels)
						{
							foreach (MixingLevel level in MixingLevels.GetLevelsExceptBase())
							{
								IEnumerable<Bob> bobsOnThisLevel = Bobs.Where(x => x.Level == level);
								if (bobsOnThisLevel.Count() <= 1)
								{
									break;
								}

								foreach (Bob bob in bobsOnThisLevel)
								{
									transaction.Outputs.AddWithOptimize(level.Denomination, bob.ActiveOutputAddress.ScriptPubKey);
								}
							}
						}

						BitcoinWitPubKeyAddress coordinatorAddress = Constants.GetCoordinatorAddress(Network);
						// 3. If there are less Bobs than Alices, then add our own address. The malicious Alice, who will refuse to sign.
						for (int i = 0; i < MixingLevels.Count(); i++)
						{
							var aliceCountInLevel = Alices.Count(x => i < x.BlindedOutputScripts.Length);
							var missingBobCount = aliceCountInLevel - Bobs.Count(x => x.Level == MixingLevels.GetLevel(i));
							for (int j = 0; j < missingBobCount; j++)
							{
								var denomination = MixingLevels.GetLevel(i).Denomination;
								transaction.Outputs.AddWithOptimize(denomination, coordinatorAddress);
							}
						}

						// 4. Start building Coordinator fee.
						var baseDenominationOutputCount = transaction.Outputs.Count(x => x.Value == newDenomination);
						Money coordinatorBaseFeePerAlice = newDenomination.Percentage(CoordinatorFeePercent * baseDenominationOutputCount);
						Money coordinatorFee = baseDenominationOutputCount * coordinatorBaseFeePerAlice;

						if (tinkerWithAdditionalMixingLevels)
						{
							foreach (MixingLevel level in MixingLevels.GetLevelsExceptBase())
							{
								var denominationOutputCount = transaction.Outputs.Count(x => x.Value == level.Denomination);
								if (denominationOutputCount <= 1)
								{
									break;
								}

								Money coordinatorLevelFeePerAlice = level.Denomination.Percentage(CoordinatorFeePercent * denominationOutputCount);
								coordinatorFee += coordinatorLevelFeePerAlice * denominationOutputCount;
							}
						}

						// 5. Add the inputs and the changes of Alices.
						var spentCoins = new List<Coin>();
						foreach (Alice alice in Alices)
						{
							foreach (var input in alice.Inputs)
							{
								transaction.Inputs.Add(new TxIn(input.Outpoint));
								spentCoins.Add(input);
							}

							Money changeAmount = alice.InputSum;
							changeAmount -= alice.NetworkFeeToPayAfterBaseDenomination;
							changeAmount -= newDenomination;
							changeAmount -= coordinatorBaseFeePerAlice;

							if (tinkerWithAdditionalMixingLevels)
							{
								for (int i = 1; i < alice.BlindedOutputScripts.Length; i++)
								{
									MixingLevel level = MixingLevels.GetLevel(i);
									var denominationOutputCount = transaction.Outputs.Count(x => x.Value == level.Denomination);
									if (denominationOutputCount <= 1)
									{
										break;
									}

									changeAmount -= FeePerOutputs;
									changeAmount -= level.Denomination;
									changeAmount -= level.Denomination.Percentage(CoordinatorFeePercent * denominationOutputCount);
								}
							}

							if (changeAmount > Money.Zero) // If the coordinator fee would make change amount to be negative or zero then no need to pay it.
							{
								Money minimumOutputAmount = Money.Coins(0.0001m); // If the change would be less than about $1 then add it to the coordinator.
								Money somePercentOfDenomination = newDenomination.Percentage(0.7m); // If the change is less than about 0.7% of the newDenomination then add it to the coordinator fee.
								Money minimumChangeAmount = Math.Max(minimumOutputAmount, somePercentOfDenomination);
								if (changeAmount < minimumChangeAmount)
								{
									coordinatorFee += changeAmount;
								}
								else
								{
									transaction.Outputs.AddWithOptimize(changeAmount, alice.ChangeOutputAddress.ScriptPubKey);
								}
							}
							else
							{
								// Alice has no money enough to pay the coordinator fee then allow her to pay what she can.
								coordinatorFee += changeAmount;
							}
						}

						// 6. Add Coordinator fee only if > about $3, else just let it to be miner fee.
						if (coordinatorFee > Money.Coins(0.0003m))
						{
							transaction.Outputs.AddWithOptimize(coordinatorFee, coordinatorAddress);
						}

						// 7. Create the unsigned transaction.
						var builder = Network.CreateTransactionBuilder();
						UnsignedCoinJoin = builder
							.ContinueToBuild(transaction)
							.AddCoins(spentCoins) // It makes sure the UnsignedCoinJoin goes through TransactionBuilder optimizations.
							.BuildTransaction(false);

						// 8. Try optimize fees.
						await TryOptimizeFeesAsync(spentCoins).ConfigureAwait(false);

						// 9. Shuffle.
						UnsignedCoinJoin.Inputs.Shuffle();
						UnsignedCoinJoin.Outputs.Shuffle();

						// 10. Sort inputs and outputs by amount so the coinjoin looks better in a block explorer.
						UnsignedCoinJoin.Inputs.SortByAmount(spentCoins);
						UnsignedCoinJoin.Outputs.SortByAmount();
						//Note: We shuffle then sort because inputs and outputs could have equal values

						SignedCoinJoin = Transaction.Parse(UnsignedCoinJoin.ToHex(), Network);

						Phase = RoundPhase.Signing;
					}
					else // Phase == RoundPhase.Signing
					{
						return;
					}

					Logger.LogInfo($"Round ({RoundId}): Phase initialized: {expectedPhase.ToString()}.");
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					Status = CoordinatorRoundStatus.Aborted;
					throw;
				}
			}

			_ = Task.Run(async () =>
				{
					try
					{
						TimeSpan timeout;
						switch (expectedPhase)
						{
							case RoundPhase.InputRegistration:
								{
									SetInputRegistrationTimesout(); // Update it, it's going to be slightly more accurate.
									timeout = InputRegistrationTimeout;
								}
								break;

							case RoundPhase.ConnectionConfirmation:
								timeout = ConnectionConfirmationTimeout;
								break;

							case RoundPhase.OutputRegistration:
								timeout = OutputRegistrationTimeout;
								break;

							case RoundPhase.Signing:
								timeout = SigningTimeout;
								break;

							default:
								throw new InvalidOperationException("This is impossible.");
						}

						// Delay asynchronously to the requested timeout.
						await Task.Delay(timeout).ConfigureAwait(false);

						var executeRunAbortion = false;
						using (await RoundSynchronizerLock.LockAsync().ConfigureAwait(false))
						{
							executeRunAbortion = Status == CoordinatorRoundStatus.Running && Phase == expectedPhase;
						}
						if (executeRunAbortion)
						{
							PhaseTimeoutLog.TryAdd((RoundId, Phase), DateTimeOffset.UtcNow);
							string timedOutLogString = $"Round ({RoundId}): {expectedPhase.ToString()} timed out after {timeout.TotalSeconds} seconds.";

							if (expectedPhase == RoundPhase.ConnectionConfirmation)
							{
								Logger.LogInfo(timedOutLogString);
							}
							else if (expectedPhase == RoundPhase.OutputRegistration)
							{
								Logger.LogInfo($"{timedOutLogString} Progressing to signing phase to blame...");
							}
							else
							{
								Logger.LogInfo($"{timedOutLogString} Aborting...");
							}

							// This will happen outside the lock.
							_ = Task.Run(async () =>
								{
									try
									{
										switch (expectedPhase)
										{
											case RoundPhase.InputRegistration:
												{
													// Only abort if less than two one Alice is registered.
													// Do not ban anyone, it's ok if they lost connection.
													await RemoveAlicesIfAnInputRefusedByMempoolAsync().ConfigureAwait(false);
													int aliceCountAfterInputRegistrationTimeout = CountAlices();
													if (aliceCountAfterInputRegistrationTimeout < 2)
													{
														Abort($"Only {aliceCountAfterInputRegistrationTimeout} Alices registered.");
													}
													else
													{
														UpdateAnonymitySet(aliceCountAfterInputRegistrationTimeout);
														// Progress to the next phase, which will be ConnectionConfirmation
														await ExecuteNextPhaseAsync(RoundPhase.ConnectionConfirmation).ConfigureAwait(false);
													}
												}
												break;

											case RoundPhase.ConnectionConfirmation:
												{
													IEnumerable<Alice> alicesToBan = GetAlicesBy(AliceState.InputsRegistered);

													await ProgressToOutputRegistrationOrFailAsync(alicesToBan.ToArray()).ConfigureAwait(false);
												}
												break;

											case RoundPhase.OutputRegistration:
												{
													// Output registration never aborts.
													// We do not know which Alice to ban.
													// Therefore proceed to signing, and whichever Alice does not sign, ban her.
													await ExecuteNextPhaseAsync(RoundPhase.Signing).ConfigureAwait(false);
												}
												break;

											case RoundPhase.Signing:
												{
													Alice[] alicesToBan = GetAlicesByNot(AliceState.SignedCoinJoin, syncLock: true).ToArray();

													if (alicesToBan.Any())
													{
														await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, RoundId, alicesToBan.SelectMany(x => x.Inputs.Select(y => y.Outpoint)).ToArray()).ConfigureAwait(false);
													}

													Abort($"{alicesToBan.Length} Alices did not sign.");
												}
												break;

											default:
												throw new InvalidOperationException("This is impossible.");
										}
									}
									catch (Exception ex)
									{
										Logger.LogWarning($"Round ({RoundId}): {expectedPhase.ToString()} timeout failed.");
										Logger.LogWarning(ex);
									}
								});
						}
					}
					catch (Exception ex)
					{
						Logger.LogWarning($"Round ({RoundId}): {expectedPhase.ToString()} timeout failed.");
						Logger.LogWarning(ex);
					}
				});
		}

		private Money CalculateNewDenomination()
		{
			// 1. Set new denomination: minor optimization.
			return Alices.Min(x => x.InputSum - x.NetworkFeeToPayAfterBaseDenomination);
		}

		public async Task ProgressToOutputRegistrationOrFailAsync(params Alice[] additionalAlicesToBan)
		{
			// Only abort if less than two one alices are registered.
			// What if an attacker registers all the time many alices, then drops out. He'll achieve only 2 alices to participate?
			// If he registers many alices at InputRegistration
			// AND never confirms in connection confirmation
			// THEN connection confirmation will go with 2 alices in every round
			// Therefore Alices that did not confirm, nor requested disconnection should be banned:

			IEnumerable<Alice> alicesToBan = await RemoveAlicesIfAnInputRefusedByMempoolAsync().ConfigureAwait(false); // So ban only those who confirmed participation, yet spent their inputs.

			IEnumerable<OutPoint> inputsToBan = alicesToBan.SelectMany(x => x.Inputs).Select(y => y.Outpoint).Concat(additionalAlicesToBan.SelectMany(x => x.Inputs).Select(y => y.Outpoint)).Distinct();

			if (inputsToBan.Any())
			{
				await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, RoundId, inputsToBan.ToArray()).ConfigureAwait(false);
			}

			RemoveAlicesBy(additionalAlicesToBan.Select(x => x.UniqueId).Concat(alicesToBan.Select(y => y.UniqueId)).Distinct().ToArray());

			int aliceCountAfterConnectionConfirmationTimeout = CountAlices();
			int didNotConfirmeCount = AnonymitySet - aliceCountAfterConnectionConfirmationTimeout;
			if (aliceCountAfterConnectionConfirmationTimeout < 2)
			{
				Abort($"{didNotConfirmeCount} Alices did not confirm their connection.");
			}
			else
			{
				if (didNotConfirmeCount > 0)
				{
					// Adjust anonymity set.
					UpdateAnonymitySet(aliceCountAfterConnectionConfirmationTimeout);
				}
				// Progress to the next phase, which will be OutputRegistration
				await ExecuteNextPhaseAsync(RoundPhase.OutputRegistration).ConfigureAwait(false);
			}
		}

		private bool CanUseAdditionalOutputs(Money newDenomination)
		{
			// If all alices only registeredone blinded output, then we cannot tinker with additional denomination.
			if (Alices.All(x => x.BlindedOutputScripts.Length == 1))
			{
				return false;
			}

			bool tinkerWithAdditionalMixingLevels = true;
			foreach (Alice alice in Alices)
			{
				// Check if inputs have enough coins.
				Money networkFeeToPay = (alice.Inputs.Count() * FeePerInputs) + (2 * FeePerOutputs);
				Money changeAmount = alice.InputSum - (newDenomination + networkFeeToPay);
				var acceptedBlindedOutputScriptsCount = 1;

				// Make sure we sign the proper number of additional blinded outputs.
				var moneySoFar = Money.Zero;
				for (int i = 1; i < alice.BlindedOutputScripts.Length; i++)
				{
					MixingLevel level = MixingLevels.GetLevel(i);
					int bobsOnThisLevel = Bobs.Count(x => x.Level == level);
					if (bobsOnThisLevel <= 1)
					{
						if (i == 1)
						{
							tinkerWithAdditionalMixingLevels = false;
						}
						break;
					}

					changeAmount -= level.Denomination + FeePerOutputs + level.Denomination.Percentage(CoordinatorFeePercent * bobsOnThisLevel);

					if (changeAmount < Money.Zero)
					{
						if (acceptedBlindedOutputScriptsCount < alice.BlindedOutputScripts.Length)
						{
							tinkerWithAdditionalMixingLevels = false;
						}
						break;
					}

					acceptedBlindedOutputScriptsCount++;
				}
			}

			return tinkerWithAdditionalMixingLevels;
		}

		public void AddRegisteredUnblindedSignature(UnblindedSignature unblindedSignature)
		{
			lock (RegisteredUnblindedSignaturesLock)
			{
				RegisteredUnblindedSignatures.Add(unblindedSignature);
			}
		}

		public bool ContainsRegisteredUnblindedSignature(UnblindedSignature unblindedSignature)
		{
			lock (RegisteredUnblindedSignaturesLock)
			{
				return RegisteredUnblindedSignatures.Any(x => x.C.Equals(unblindedSignature.C) && x.S.Equals(unblindedSignature.S));
			}
		}

		public int EstimateBestMixingLevel(Alice alice)
		{
			Money newDenomination = CalculateNewDenomination();

			// Check if inputs have enough coins.
			Money networkFeeToPay = (alice.Inputs.Count() * FeePerInputs) + (2 * FeePerOutputs);
			Money changeAmount = alice.InputSum - (newDenomination + networkFeeToPay);
			var acceptedBlindedOutputScriptsCount = 1;

			// Make sure we sign the proper number of additional blinded outputs.
			var moneySoFar = Money.Zero;
			for (int i = 1; i < alice.BlindedOutputScripts.Length; i++)
			{
				MixingLevel level = MixingLevels.GetLevel(i);
				var potentialAlicesOnThisLevel = Alices.Count(x => x.BlindedOutputSignatures.Length > i);
				if (potentialAlicesOnThisLevel <= 1)
				{
					break;
				}

				changeAmount -= level.Denomination + FeePerOutputs + level.Denomination.Percentage(CoordinatorFeePercent * potentialAlicesOnThisLevel);

				if (changeAmount < Money.Zero)
				{
					break;
				}

				acceptedBlindedOutputScriptsCount++;
			}

			return acceptedBlindedOutputScriptsCount;
		}

		private async Task TryOptimizeFeesAsync(IEnumerable<Coin> spentCoins)
		{
			try
			{
				await TryOptimizeConfirmationTargetAsync(spentCoins.Select(x => x.Outpoint.Hash).ToHashSet()).ConfigureAwait(false);

				// 8.1. Estimate the current FeeRate. Note, there are no signatures yet!
				int estimatedSigSizeBytes = UnsignedCoinJoin.Inputs.Count * Constants.P2wpkhInputSizeInBytes;
				int estimatedFinalTxSize = UnsignedCoinJoin.GetSerializedSize() + estimatedSigSizeBytes;
				Money fee = UnsignedCoinJoin.GetFee(spentCoins.ToArray());

				// There is a currentFeeRate null check later.
				FeeRate currentFeeRate = null;
				if (fee is null)
				{
					Logger.LogError($"Cannot calculate CoinJoin transaction fee. Some spent coins are missing.");
				}
				else if (fee <= Money.Zero)
				{
					Logger.LogError("CoinJoin transaction is not paying any fee.");
				}
				else
				{
					currentFeeRate = new FeeRate(fee, estimatedFinalTxSize);
				}

				// 8.2. Get the most optimal FeeRate.
				EstimateSmartFeeResponse estimateSmartFeeResponse = await RpcClient.EstimateSmartFeeAsync(AdjustedConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true).ConfigureAwait(false);
				if (estimateSmartFeeResponse is null)
				{
					throw new InvalidOperationException($"{nameof(FeeRate)} is not yet initialized.");
				}

				FeeRate optimalFeeRate = estimateSmartFeeResponse.FeeRate;

				if (optimalFeeRate != null && optimalFeeRate != FeeRate.Zero && currentFeeRate != null && currentFeeRate != FeeRate.Zero) // This would be really strange if it'd happen.
				{
					var sanityFeeRate = new FeeRate(2m); // 2 s/b
					optimalFeeRate = optimalFeeRate < sanityFeeRate ? sanityFeeRate : optimalFeeRate;
					if (optimalFeeRate < currentFeeRate)
					{
						// 8.2 If the fee can be lowered, lower it.
						// 8.2.1. How much fee can we save?
						Money feeShouldBePaid = Money.Satoshis(estimatedFinalTxSize * (int)optimalFeeRate.SatoshiPerByte);
						Money toSave = fee - feeShouldBePaid;

						// 8.2.2. Get the outputs to divide the savings between.
						int maxMixCount = UnsignedCoinJoin.GetIndistinguishableOutputs(includeSingle: true).Max(x => x.count);
						Money bestMixAmount = UnsignedCoinJoin.GetIndistinguishableOutputs(includeSingle: true).Where(x => x.count == maxMixCount).Max(x => x.value);
						int bestMixCount = UnsignedCoinJoin.GetIndistinguishableOutputs(includeSingle: true).First(x => x.value == bestMixAmount).count;

						// 8.2.3. Get the savings per best mix outputs.
						long toSavePerBestMixOutputs = toSave.Satoshi / bestMixCount;

						// 8.2.4. Modify the best mix outputs in the transaction.
						if (toSavePerBestMixOutputs > 0)
						{
							foreach (TxOut output in UnsignedCoinJoin.Outputs.Where(x => x.Value == bestMixAmount))
							{
								output.Value += toSavePerBestMixOutputs;
							}
						}
					}
				}
				else
				{
					Logger.LogError($"This is impossible. {nameof(optimalFeeRate)}: {optimalFeeRate}, {nameof(currentFeeRate)}: {currentFeeRate}.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Failed to optimize fees. Fallback to normal fees.");
				Logger.LogWarning(ex);
			}
		}

		private async Task TryOptimizeConfirmationTargetAsync(ISet<uint256> transactionHashes)
		{
			try
			{
				// If the transaction does not spend unconfirmed coins then the confirmation target can be the one that's been set in the config.
				var originalConfirmationTarget = AdjustedConfirmationTarget;

				// Note that only dependents matter, spenders do not matter much or at all, they just allow this transaction to be confirmed faster.
				var dependents = await RpcClient.GetAllDependentsAsync(transactionHashes, includingProvided: true, likelyProvidedManyConfirmedOnes: true).ConfigureAwait(false);
				AdjustedConfirmationTarget = AdjustConfirmationTarget(dependents.Count, ConfiguredConfirmationTarget, ConfiguredConfirmationTargetReductionRate);

				if (originalConfirmationTarget != AdjustedConfirmationTarget)
				{
					Logger.LogInfo($"Confirmation target is optimized from {originalConfirmationTarget} blocks to {AdjustedConfirmationTarget} blocks.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Failed to optimize confirmation target. Fallback using the original one: {AdjustedConfirmationTarget} blocks.");
				Logger.LogWarning(ex);
			}
		}

		public static async Task<(Money feePerInputs, Money feePerOutputs)> CalculateFeesAsync(RPCClient rpc, int confirmationTarget)
		{
			Guard.NotNull(nameof(rpc), rpc);
			Guard.NotNull(nameof(confirmationTarget), confirmationTarget);

			Money feePerInputs = null;
			Money feePerOutputs = null;
			var inputSizeInBytes = (int)Math.Ceiling(((3 * Constants.P2wpkhInputSizeInBytes) + Constants.P2pkhInputSizeInBytes) / 4m);
			var outputSizeInBytes = Constants.OutputSizeInBytes;
			try
			{
				var estimateSmartFeeResponse = await rpc.EstimateSmartFeeAsync(confirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true).ConfigureAwait(false);
				if (estimateSmartFeeResponse is null)
				{
					throw new InvalidOperationException($"{nameof(FeeRate)} is not yet initialized.");
				}

				var feeRate = estimateSmartFeeResponse.FeeRate;
				Money feePerBytes = feeRate.FeePerK / 1000;

				// Make sure min relay fee (1000 sat) is hit.
				feePerInputs = Math.Max(feePerBytes * inputSizeInBytes, Money.Satoshis(500));
				feePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, Money.Satoshis(250));
			}
			catch (Exception ex)
			{
				// If fee has not been initialized once, fall back.
				if (feePerInputs is null || feePerOutputs is null)
				{
					var feePerBytes = Money.Satoshis(100); // 100 satoshi per byte

					// Make sure min relay fee (1000 sat) is hit.
					feePerInputs = Math.Max(feePerBytes * inputSizeInBytes, Money.Satoshis(500));
					feePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, Money.Satoshis(250));
				}

				Logger.LogError(ex);
			}

			return (feePerInputs, feePerOutputs);
		}

		public void Succeed(bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					Status = CoordinatorRoundStatus.Succeded;
				}
			}
			else
			{
				Status = CoordinatorRoundStatus.Succeded;
			}
			Logger.LogInfo($"Round ({RoundId}): Succeeded.");
		}

		public void Abort(string reason, bool syncLock = true, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					Status = CoordinatorRoundStatus.Aborted;
				}
			}
			else
			{
				Status = CoordinatorRoundStatus.Aborted;
			}

			if (string.IsNullOrWhiteSpace(reason))
			{
				Logger.LogInfo($"Round ({RoundId}): Aborted.", callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
			}
			else
			{
				Logger.LogInfo($"Round ({RoundId}): Aborted. Reason: {reason}.", callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
			}
		}

		public int CountAlices(bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					return Alices.Count;
				}
			}
			return Alices.Count;
		}

		public bool AllAlices(AliceState state)
		{
			using (RoundSynchronizerLock.Lock())
			{
				return Alices.All(x => x.State == state);
			}
		}

		public int CountBlindSignatures()
		{
			using (RoundSynchronizerLock.Lock())
			{
				return Alices.Sum(x => x.BlindedOutputSignatures.Length);
			}
		}

		public bool ContainsBlindedOutputScript(uint256 blindedOutputScript)
		{
			using (RoundSynchronizerLock.Lock())
			{
				foreach (Alice alice in Alices)
				{
					if (alice.BlindedOutputScripts.Contains(blindedOutputScript))
					{
						return true;
					}
				}
			}

			return false;
		}

		public bool ContainsAnyBlindedOutputScript(IEnumerable<uint256> blindedOutputScripts)
		{
			using (RoundSynchronizerLock.Lock())
			{
				foreach (var bis in blindedOutputScripts)
				{
					foreach (Alice alice in Alices)
					{
						if (alice.BlindedOutputScripts.Contains(bis))
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		public bool ContainsInput(OutPoint input, out List<Alice> alices)
		{
			alices = new List<Alice>();

			using (RoundSynchronizerLock.Lock())
			{
				foreach (Alice alice in Alices)
				{
					if (alice.Inputs.Any(x => x.Outpoint == input))
					{
						alices.Add(alice);
					}
				}
			}

			return alices.Count > 0;
		}

		public int CountBobs(bool synchronized = true)
		{
			if (synchronized)
			{
				using (RoundSynchronizerLock.Lock())
				{
					return Bobs.Count;
				}
			}
			return Bobs.Count;
		}

		public IEnumerable<Alice> GetAlicesBy(AliceState state)
		{
			using (RoundSynchronizerLock.Lock())
			{
				return Alices.Where(x => x.State == state).ToList();
			}
		}

		public Alice TryGetAliceBy(Guid uniqueId)
		{
			using (RoundSynchronizerLock.Lock())
			{
				return Alices.SingleOrDefault(x => x.UniqueId == uniqueId);
			}
		}

		public IEnumerable<Alice> GetAlicesByNot(AliceState state, bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					return Alices.Where(x => x.State != state).ToList();
				}
			}
			return Alices.Where(x => x.State != state).ToList();
		}

		public void StartAliceTimeout(Guid uniqueId)
		{
			// 1. Find Alice and set its LastSeen propery.
			var foundAlice = false;
			var started = DateTimeOffset.UtcNow;
			using (RoundSynchronizerLock.Lock())
			{
				if (Phase != RoundPhase.InputRegistration || Status != CoordinatorRoundStatus.Running)
				{
					return; // Then no need to timeout alice.
				}

				Alice alice = Alices.SingleOrDefault(x => x.UniqueId == uniqueId);
				foundAlice = alice != default(Alice);
				if (foundAlice)
				{
					alice.LastSeen = started;
				}
			}

			if (foundAlice)
			{
				Task.Run(async () =>
				{
					// 2. Delay asynchronously to the requested timeout
					await Task.Delay(AliceRegistrationTimeout).ConfigureAwait(false);

					using (await RoundSynchronizerLock.LockAsync().ConfigureAwait(false))
					{
						// 3. If the round is still running and the phase is still InputRegistration
						if (Status == CoordinatorRoundStatus.Running && Phase == RoundPhase.InputRegistration)
						{
							Alice alice = Alices.SingleOrDefault(x => x.UniqueId == uniqueId);
							if (alice != default(Alice))
							{
								// 4. If LastSeen is not changed by then, remove Alice.
								if (alice.LastSeen == started)
								{
									Alices.Remove(alice);
									Logger.LogInfo($"Round ({RoundId}): Alice ({alice.UniqueId}) timed out.");
								}
							}
						}
					}
				});
			}
		}

		public static int AdjustConfirmationTarget(int unconfirmedCount, int startingConfirmationTarget, double confirmationTargetReductionRate)
		{
			for (int i = 0; i < unconfirmedCount; i++)
			{
				startingConfirmationTarget = (int)(startingConfirmationTarget * confirmationTargetReductionRate);
			}

			startingConfirmationTarget = Math.Max(startingConfirmationTarget, 2); // Conf target should never be less than 2.
			return startingConfirmationTarget;
		}

		#region Modifiers

		public async Task BroadcastCoinJoinIfFullySignedAsync()
		{
			using (await RoundSynchronizerLock.LockAsync().ConfigureAwait(false))
			{
				// Check if fully signed.
				if (SignedCoinJoin.Inputs.All(x => x.HasWitScript()))
				{
					Logger.LogInfo($"Round ({RoundId}): Trying to broadcast coinjoin.");

					try
					{
						Coin[] spentCoins = Alices.SelectMany(x => x.Inputs).ToArray();
						Money networkFee = SignedCoinJoin.GetFee(spentCoins);
						Logger.LogInfo($"Round ({RoundId}): Network Fee: {networkFee.ToString(false, false)} BTC.");
						Logger.LogInfo($"Round ({RoundId}): Coordinator Fee: {SignedCoinJoin.Outputs.SingleOrDefault(x => x.ScriptPubKey == Constants.GetCoordinatorAddress(Network).ScriptPubKey)?.Value?.ToString(false, false) ?? "0"} BTC.");
						FeeRate feeRate = SignedCoinJoin.GetFeeRate(spentCoins);
						Logger.LogInfo($"Round ({RoundId}): Network Fee Rate: {feeRate.FeePerK.ToDecimal(MoneyUnit.Satoshi) / 1000} sat/byte.");
						Logger.LogInfo($"Round ({RoundId}): Number of inputs: {SignedCoinJoin.Inputs.Count}.");
						Logger.LogInfo($"Round ({RoundId}): Number of outputs: {SignedCoinJoin.Outputs.Count}.");
						Logger.LogInfo($"Round ({RoundId}): Serialized Size: {SignedCoinJoin.GetSerializedSize() / 1024} KB.");
						Logger.LogInfo($"Round ({RoundId}): VSize: {SignedCoinJoin.GetVirtualSize() / 1024} KB.");
						foreach (var o in SignedCoinJoin.GetIndistinguishableOutputs(includeSingle: false))
						{
							Logger.LogInfo($"Round ({RoundId}): There are {o.count} occurrences of {o.value.ToString(true, false)} BTC output.");
						}

						await RpcClient.SendRawTransactionAsync(SignedCoinJoin).ConfigureAwait(false);
						CoinJoinBroadcasted?.Invoke(this, SignedCoinJoin);
						Succeed(syncLock: false);
						Logger.LogInfo($"Round ({RoundId}): Successfully broadcasted the CoinJoin: {SignedCoinJoin.GetHash()}.");
					}
					catch (Exception ex)
					{
						Abort($"Could not broadcast the CoinJoin: {SignedCoinJoin.GetHash()}.", syncLock: false);
						Logger.LogError(ex);
					}
				}
			}
		}

		public void UpdateAnonymitySet(int anonymitySet, bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					if ((Phase != RoundPhase.InputRegistration && Phase != RoundPhase.ConnectionConfirmation) || Status != CoordinatorRoundStatus.Running)
					{
						throw new InvalidOperationException($"Updating anonymity set is not allowed in {Phase.ToString()} phase.");
					}
					AnonymitySet = anonymitySet;
				}
			}
			else
			{
				if ((Phase != RoundPhase.InputRegistration && Phase != RoundPhase.ConnectionConfirmation) || Status != CoordinatorRoundStatus.Running)
				{
					throw new InvalidOperationException($"Updating anonymity set is not allowed in {Phase.ToString()} phase.");
				}
				AnonymitySet = anonymitySet;
			}
			Logger.LogInfo($"Round ({RoundId}): {nameof(AnonymitySet)} updated: {AnonymitySet}.");
		}

		public void AddAlice(Alice alice)
		{
			using (RoundSynchronizerLock.Lock())
			{
				if (Phase != RoundPhase.InputRegistration || Status != CoordinatorRoundStatus.Running)
				{
					throw new InvalidOperationException("Adding Alice is only allowed in InputRegistration phase.");
				}
				Alices.Add(alice);
			}

			StartAliceTimeout(alice.UniqueId);

			Logger.LogInfo($"Round ({RoundId}): Alice ({alice.InputSum.ToString(false, false)}) added.");
		}

		public void AddBob(Bob bob)
		{
			using (RoundSynchronizerLock.Lock())
			{
				if (Phase != RoundPhase.OutputRegistration || Status != CoordinatorRoundStatus.Running)
				{
					throw new InvalidOperationException("Adding Bob is only allowed in OutputRegistration phase.");
				}

				// If Bob is already added with the same scriptpubkey and level, that's fine.
				Bobs.Add(bob);
			}

			Logger.LogInfo($"Round ({RoundId}): Bob ({bob.Level.Denomination}) added.");
		}

		public async Task<IEnumerable<Alice>> RemoveAlicesIfAnInputRefusedByMempoolAsync()
		{
			var alicesRemoved = new List<Alice>();
			var key = new Key();

			using (await RoundSynchronizerLock.LockAsync().ConfigureAwait(false))
			{
				if ((Phase != RoundPhase.InputRegistration && Phase != RoundPhase.ConnectionConfirmation) || Status != CoordinatorRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}

				var checkingTasks = new List<(Alice alice, Task<(bool accepted, string reason)> task)>();
				var batch = RpcClient.PrepareBatch();
				foreach (Alice alice in Alices)
				{
					// Check if mempool would accept a fake transaction created with the registered inputs.
					// This will catch ascendant/descendant count and size limits for example.
					checkingTasks.Add((alice, batch.TestMempoolAcceptAsync(alice.Inputs)));
				}
				var waiting = Task.WhenAll(checkingTasks.Select(t => t.task));
				await batch.SendBatchAsync().ConfigureAwait(false);
				await waiting.ConfigureAwait(false);

				foreach (var t in checkingTasks)
				{
					var result = await t.task.ConfigureAwait(false);
					if (!result.accepted)
					{
						alicesRemoved.Add(t.alice);
						Alices.Remove(t.alice);
					}
				}
			}

			foreach (var alice in alicesRemoved)
			{
				Logger.LogInfo($"Round ({RoundId}): Alice ({alice.UniqueId}) removed.");
			}

			return alicesRemoved;
		}

		public int RemoveAlicesBy(params Guid[] ids)
		{
			var numberOfRemovedAlices = 0;
			using (RoundSynchronizerLock.Lock())
			{
				if ((Phase != RoundPhase.InputRegistration && Phase != RoundPhase.ConnectionConfirmation) || Status != CoordinatorRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				foreach (var id in ids)
				{
					numberOfRemovedAlices = Alices.RemoveAll(x => x.UniqueId == id);
				}
			}

			if (numberOfRemovedAlices > 0)
			{
				Logger.LogInfo($"Round ({RoundId}): {numberOfRemovedAlices} alices are removed.");
			}

			return numberOfRemovedAlices;
		}

		#endregion Modifiers
	}
}
