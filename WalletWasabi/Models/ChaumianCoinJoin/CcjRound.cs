using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class CcjRound
	{
		public static long RoundCount;
		public long RoundId { get; }

		public RPCClient RpcClient { get; }
		public Network Network => RpcClient.Network;

		public Money Denomination { get; }
		public int ConfirmationTarget { get; }
		public decimal CoordinatorFeePercent { get; }
		public int AnonymitySet { get; private set; }

		public Money FeePerInputs { get; private set; }
		public Money FeePerOutputs { get; private set; }

		public ECDSABlinding.Signer Signer { get; private set; }
		public string RoundHash { get; private set; }

		public Transaction UnsignedCoinJoin { get; private set; }
		private string _unsignedCoinJoinHex;

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
		private List<Bob> Bobs { get; }

		private static AsyncLock RoundSynchronizerLock { get; } = new AsyncLock();

		private CcjRoundPhase _phase;
		private object PhaseLock { get; }

		public CcjRoundPhase Phase
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

		public event EventHandler<CcjRoundPhase> PhaseChanged;

		private CcjRoundStatus _status;

		private object StatusLock { get; }

		public CcjRoundStatus Status
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

		public event EventHandler<CcjRoundStatus> StatusChanged;

		public event EventHandler<Transaction> CoinJoinBroadcasted;

		public TimeSpan AliceRegistrationTimeout => ConnectionConfirmationTimeout;

		public TimeSpan InputRegistrationTimeout { get; }

		public TimeSpan ConnectionConfirmationTimeout { get; }

		public TimeSpan OutputRegistrationTimeout { get; }

		public TimeSpan SigningTimeout { get; }

		public UtxoReferee UtxoReferee { get; }

		public CcjRound(RPCClient rpc, UtxoReferee utxoReferee, CcjRoundConfig config)
		{
			try
			{
				Interlocked.Increment(ref RoundCount);
				RoundId = Interlocked.Read(ref RoundCount);

				RpcClient = Guard.NotNull(nameof(rpc), rpc);
				UtxoReferee = Guard.NotNull(nameof(utxoReferee), utxoReferee);
				Guard.NotNull(nameof(config), config);

				Denomination = config.Denomination;
				ConfirmationTarget = (int)config.ConfirmationTarget;
				CoordinatorFeePercent = (decimal)config.CoordinatorFeePercent;
				AnonymitySet = (int)config.AnonymitySet;
				InputRegistrationTimeout = TimeSpan.FromSeconds((long)config.InputRegistrationTimeout);
				ConnectionConfirmationTimeout = TimeSpan.FromSeconds((long)config.ConnectionConfirmationTimeout);
				OutputRegistrationTimeout = TimeSpan.FromSeconds((long)config.OutputRegistrationTimeout);
				SigningTimeout = TimeSpan.FromSeconds((long)config.SigningTimeout);

				PhaseLock = new object();
				Phase = CcjRoundPhase.InputRegistration;
				StatusLock = new object();
				Status = CcjRoundStatus.NotStarted;

				Signer = new ECDSABlinding.Signer(new Key());
				RoundHash = null;
				_unsignedCoinJoinHex = null;

				UnsignedCoinJoin = null;
				SignedCoinJoin = null;

				Alices = new List<Alice>();
				Bobs = new List<Bob>();

				Logger.LogInfo<CcjRound>($"New round ({RoundId}) is created.\n\t" +
					$"{nameof(Denomination)}: {Denomination.ToString(false, true)} BTC.\n\t" +
					$"{nameof(ConfirmationTarget)}: {ConfirmationTarget}.\n\t" +
					$"{nameof(CoordinatorFeePercent)}: {CoordinatorFeePercent}%.\n\t" +
					$"{nameof(AnonymitySet)}: {AnonymitySet}.");
			}
			catch (Exception ex)
			{
				Logger.LogError<CcjRound>($"Round ({RoundId}): Could not create.");
				Logger.LogError<CcjRound>(ex);
				throw;
			}
		}

		public async Task ExecuteNextPhaseAsync(CcjRoundPhase expectedPhase, Money feePerInputs = null, Money feePerOutputs = null)
		{
			using (await RoundSynchronizerLock.LockAsync())
			{
				try
				{
					Logger.LogInfo<CcjRound>($"Round ({RoundId}): Phase change requested: {expectedPhase.ToString()}.");

					if (Status == CcjRoundStatus.NotStarted) // So start the input registration phase
					{
						if (expectedPhase != CcjRoundPhase.InputRegistration)
						{
							return;
						}

						// Calculate fees.
						if (feePerInputs is null || feePerOutputs is null)
						{
							(Money feePerInputs, Money feePerOutputs) fees = await CalculateFeesAsync(RpcClient, ConfirmationTarget);
							FeePerInputs = feePerInputs ?? fees.feePerInputs;
							FeePerOutputs = feePerOutputs ?? fees.feePerOutputs;
						}
						else
						{
							FeePerInputs = feePerInputs;
							FeePerOutputs = feePerOutputs;
						}

						Status = CcjRoundStatus.Running;
					}
					else if (Status != CcjRoundStatus.Running) // Aborted or succeeded, swallow
					{
						return;
					}
					else if (Phase == CcjRoundPhase.InputRegistration)
					{
						if (expectedPhase != CcjRoundPhase.ConnectionConfirmation)
						{
							return;
						}

						RoundHash = NBitcoinHelpers.HashOutpoints(Alices.SelectMany(x => x.Inputs).Select(y => y.Outpoint));

						Phase = CcjRoundPhase.ConnectionConfirmation;
					}
					else if (Phase == CcjRoundPhase.ConnectionConfirmation)
					{
						if (expectedPhase != CcjRoundPhase.OutputRegistration)
						{
							return;
						}

						Phase = CcjRoundPhase.OutputRegistration;
					}
					else if (Phase == CcjRoundPhase.OutputRegistration)
					{
						if (expectedPhase != CcjRoundPhase.Signing)
						{
							return;
						}

						// Build CoinJoin

						// 1. Set new denomination: minor optimization.
						Money newDenomination = Alices.Min(x => x.OutputSumWithoutCoordinatorFeeAndDenomination);
						var transaction = Network.Consensus.ConsensusFactory.CreateTransaction();

						// 2. Add Bob outputs.
						foreach (Bob bob in Bobs)
						{
							transaction.Outputs.Add(newDenomination, bob.ActiveOutputAddress.ScriptPubKey);
						}

						BitcoinWitPubKeyAddress coordinatorAddress = Constants.GetCoordinatorAddress(Network);
						// 3. If there are less Bobs than Alices, then add our own address. The malicious Alice, who will refuse to sign.
						for (int i = 0; i < Alices.Count - Bobs.Count; i++)
						{
							transaction.Outputs.Add(newDenomination, coordinatorAddress);
						}

						// 4. Start building Coordinator fee.
						Money coordinatorFeePerAlice = newDenomination.Percentange(CoordinatorFeePercent) * Alices.Count;
						Money coordinatorFee = Alices.Count * coordinatorFeePerAlice;

						// 5. Add the inputs and the changes of Alices.
						var spentCoins = new List<Coin>();
						foreach (Alice alice in Alices)
						{
							foreach (var input in alice.Inputs)
							{
								transaction.Inputs.Add(new TxIn(input.Outpoint));
								spentCoins.Add(input);
							}
							Money changeAmount = alice.GetChangeAmount(newDenomination, coordinatorFeePerAlice);
							if (changeAmount > Money.Zero) // If the coordinator fee would make change amount to be negative or zero then no need to pay it.
							{
								Money minimumOutputAmount = Money.Coins(0.0001m); // If the change would be less than about $1 then add it to the coordinator.
								Money onePercentOfDenomination = newDenomination.Percentange(1m); // If the change is less than about 1% of the newDenomination then add it to the coordinator fee.
								Money minimumChangeAmount = Math.Max(minimumOutputAmount, onePercentOfDenomination);
								if (changeAmount < minimumChangeAmount)
								{
									coordinatorFee += changeAmount;
								}
								else
								{
									transaction.Outputs.Add(changeAmount, alice.ChangeOutputAddress.ScriptPubKey);
								}
							}
							else
							{
								coordinatorFee -= coordinatorFeePerAlice;
							}
						}

						// 6. Add Coordinator fee only if > about $3, else just let it to be miner fee.
						if (coordinatorFee > Money.Coins(0.0003m))
						{
							transaction.Outputs.Add(coordinatorFee, coordinatorAddress);
						}

						// 7. Create the unsigned transaction.
						var builder = Network.CreateTransactionBuilder();
						UnsignedCoinJoin = builder
							.ContinueToBuild(transaction)
							.AddCoins(spentCoins) // It makes sure the UnsignedCoinJoin goes through TransactionBuilder optimizations.
							.BuildTransaction(false);

						// 8. Try optimize fees.
						try
						{
							// 8.1. Estimate the current FeeRate. Note, there are no signatures yet!
							int estimatedSigSizeBytes = UnsignedCoinJoin.Inputs.Count * Constants.P2wpkhInputSizeInBytes;
							int estimatedFinalTxSize = UnsignedCoinJoin.GetSerializedSize() + estimatedSigSizeBytes;
							Money fee = UnsignedCoinJoin.GetFee(spentCoins.ToArray());
							// There is a currentFeeRate null check later.
							FeeRate currentFeeRate = fee is null ? null : new FeeRate(fee, estimatedFinalTxSize);

							// 8.2. Get the most optimal FeeRate.
							EstimateSmartFeeResponse estimateSmartFeeResponse = await RpcClient.EstimateSmartFeeAsync(ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true);
							if (estimateSmartFeeResponse is null) throw new InvalidOperationException("FeeRate is not yet initialized");
							FeeRate optimalFeeRate = estimateSmartFeeResponse.FeeRate;

							if (!(optimalFeeRate is null) && optimalFeeRate != FeeRate.Zero && !(currentFeeRate is null) && currentFeeRate != FeeRate.Zero) // This would be really strange if it'd happen.
							{
								var sanityFeeRate = new FeeRate(2m); // 2 s/b
								optimalFeeRate = optimalFeeRate < sanityFeeRate ? sanityFeeRate : optimalFeeRate;
								if (optimalFeeRate < currentFeeRate)
								{
									// 8.2 If the fee can be lowered, lower it.
									// 8.2.1. How much fee can we save?
									Money feeShouldBePaid = new Money(estimatedFinalTxSize * (int)optimalFeeRate.SatoshiPerByte);
									Money toSave = fee - feeShouldBePaid;

									// 8.2.2. Get the outputs to divide  the savings between.
									int maxMixCount = UnsignedCoinJoin.GetIndistinguishableOutputs().Max(x => x.count);
									Money bestMixAmount = UnsignedCoinJoin.GetIndistinguishableOutputs().Where(x => x.count == maxMixCount).Max(x => x.value);
									int bestMixCount = UnsignedCoinJoin.GetIndistinguishableOutputs().First(x => x.value == bestMixAmount).count;

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
								Logger.LogError<CcjRound>($"This is impossible. {nameof(optimalFeeRate)}: {optimalFeeRate}, {nameof(currentFeeRate)}: {currentFeeRate}.");
							}
						}
						catch (Exception ex)
						{
							Logger.LogWarning<CcjRound>("Couldn't optimize fees. Fallback to normal fees.");
							Logger.LogWarning<CcjRound>(ex);
						}

						SignedCoinJoin = Transaction.Parse(UnsignedCoinJoin.ToHex(), Network);

						Phase = CcjRoundPhase.Signing;
					}
					else
					{
						return;
					}

					Logger.LogInfo<CcjRound>($"Round ({RoundId}): Phase initialized: {expectedPhase.ToString()}.");
				}
				catch (Exception ex)
				{
					Logger.LogError<CcjRound>(ex);
					Status = CcjRoundStatus.Aborted;
					throw;
				}
			}

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Task.Run(async () =>
			{
				TimeSpan timeout;
				switch (expectedPhase)
				{
					case CcjRoundPhase.InputRegistration:
						timeout = InputRegistrationTimeout;
						break;

					case CcjRoundPhase.ConnectionConfirmation:
						timeout = ConnectionConfirmationTimeout;
						break;

					case CcjRoundPhase.OutputRegistration:
						timeout = OutputRegistrationTimeout;
						break;

					case CcjRoundPhase.Signing:
						timeout = SigningTimeout;
						break;

					default: throw new InvalidOperationException("This is impossible to happen.");
				}

				// Delay asynchronously to the requested timeout.
				await Task.Delay(timeout);

				var executeRunAbortion = false;
				using (await RoundSynchronizerLock.LockAsync())
				{
					executeRunAbortion = Status == CcjRoundStatus.Running && Phase == expectedPhase;
				}
				if (executeRunAbortion)
				{
					Logger.LogInfo<CcjRound>($"Round ({RoundId}): {expectedPhase.ToString()} timed out after {timeout.TotalSeconds} seconds. Aborting...");

					// This will happen outside the lock.
					Task.Run(async () =>
					{
						try
						{
							switch (expectedPhase)
							{
								case CcjRoundPhase.InputRegistration:
									{
										// Only abort if less than two one Alice is registered.
										// Don't ban anyone, it's ok if they lost connection.
										await RemoveAlicesIfAnInputRefusedByMempoolAsync();
										int aliceCountAfterInputRegistrationTimeout = CountAlices();
										if (aliceCountAfterInputRegistrationTimeout < 2)
										{
											Abort(nameof(CcjRound), $"Only {aliceCountAfterInputRegistrationTimeout} Alices registered.");
										}
										else
										{
											UpdateAnonymitySet(aliceCountAfterInputRegistrationTimeout);
											// Progress to the next phase, which will be ConnectionConfirmation
											await ExecuteNextPhaseAsync(CcjRoundPhase.ConnectionConfirmation);
										}
									}
									break;

								case CcjRoundPhase.ConnectionConfirmation:
									{
										// Only abort if less than two one alices are registered.
										// What if an attacker registers all the time many alices, then drops out. He'll achieve only 2 alices to participate?
										// If he registers many alices at InputRegistration
										// AND never confirms in connection confirmation
										// THEN connection confirmation will go with 2 alices in every round
										// Therefore Alices those didn't confirm, nor requested dsconnection should be banned:
										IEnumerable<Alice> alicesToBan1 = GetAlicesBy(AliceState.InputsRegistered);
										IEnumerable<Alice> alicesToBan2 = await RemoveAlicesIfAnInputRefusedByMempoolAsync(); // So ban only those who confirmed participation, yet spent their inputs.

										IEnumerable<OutPoint> inputsToBan = alicesToBan1.SelectMany(x => x.Inputs).Select(y => y.Outpoint).Concat(alicesToBan2.SelectMany(x => x.Inputs).Select(y => y.Outpoint)).Distinct();

										if (inputsToBan.Any())
										{
											await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, RoundId, inputsToBan.ToArray());
										}

										RemoveAlicesBy(alicesToBan1.Select(x => x.UniqueId).Concat(alicesToBan2.Select(y => y.UniqueId)).Distinct().ToArray());

										int aliceCountAfterConnectionConfirmationTimeout = CountAlices();
										int didNotConfirmeCount = AnonymitySet - aliceCountAfterConnectionConfirmationTimeout;
										if (didNotConfirmeCount > 0)
										{
											Abort(nameof(CcjRound), $"{didNotConfirmeCount} Alices did not confirem their connection.");
										}
										else
										{
											// Progress to the next phase, which will be OutputRegistration
											await ExecuteNextPhaseAsync(CcjRoundPhase.OutputRegistration);
										}
									}
									break;

								case CcjRoundPhase.OutputRegistration:
									{
										// Output registration never aborts.
										// We don't know which Alice to ban.
										// Therefore proceed to signing, and whichever Alice doesn't sign, ban her.
										await ExecuteNextPhaseAsync(CcjRoundPhase.Signing);
									}
									break;

								case CcjRoundPhase.Signing:
									{
										var outpointsToBan = new List<OutPoint>();
										using (await RoundSynchronizerLock.LockAsync())
										{
											foreach (Alice alice in Alices)
											{
												if (alice.State != AliceState.SignedCoinJoin)
												{
													outpointsToBan.AddRange(alice.Inputs.Select(x => x.Outpoint));
												}
											}
										}
										if (outpointsToBan.Any())
										{
											await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, RoundId, outpointsToBan.ToArray());
										}
										Abort(nameof(CcjRound), "Not all Alices signed.");
									}
									break;

								default: throw new InvalidOperationException("This is impossible to happen.");
							}
						}
						catch (Exception ex)
						{
							Logger.LogWarning<CcjRound>($"Round ({RoundId}): {expectedPhase.ToString()} timeout failed with exception: {ex}");
						}
					});
				}
			});
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
				var estimateSmartFeeResponse = await rpc.EstimateSmartFeeAsync(confirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true);
				if (estimateSmartFeeResponse is null) throw new InvalidOperationException("FeeRate is not yet initialized");
				var feeRate = estimateSmartFeeResponse.FeeRate;
				Money feePerBytes = (feeRate.FeePerK / 1000);

				// Make sure min relay fee (1000 sat) is hit.
				feePerInputs = Math.Max(feePerBytes * inputSizeInBytes, new Money(500));
				feePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, new Money(250));
			}
			catch (Exception ex)
			{
				// If fee hasn't been initialized once, fall back.
				if (feePerInputs is null || feePerOutputs is null)
				{
					var feePerBytes = new Money(100); // 100 satoshi per byte

					// Make sure min relay fee (1000 sat) is hit.
					feePerInputs = Math.Max(feePerBytes * inputSizeInBytes, new Money(500));
					feePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, new Money(250));
				}

				Logger.LogError<CcjRound>(ex);
			}

			return (feePerInputs, feePerOutputs);
		}

		public void Succeed(bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					Status = CcjRoundStatus.Succeded;
				}
			}
			else
			{
				Status = CcjRoundStatus.Succeded;
			}
			Logger.LogInfo<CcjRound>($"Round ({RoundId}): Succeeded.");
		}

		public void Abort(string loggingCategory, string reason, bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundSynchronizerLock.Lock())
				{
					Status = CcjRoundStatus.Aborted;
				}
			}
			else
			{
				Status = CcjRoundStatus.Aborted;
			}

			string category = string.IsNullOrWhiteSpace(loggingCategory) ? loggingCategory : nameof(CcjRound);

			if (string.IsNullOrWhiteSpace(reason))
			{
				Logger.LogInfo($"Round ({RoundId}): Aborted.", category);
			}
			else
			{
				Logger.LogInfo($"Round ({RoundId}): Aborted. Reason: {reason}", category);
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

		public bool ContainsBlindedOutputScriptHex(string blindedOutputScriptHex, out List<Alice> alices)
		{
			alices = new List<Alice>();

			using (RoundSynchronizerLock.Lock())
			{
				foreach (Alice alice in Alices)
				{
					if (alice.BlindedOutputScriptHex == blindedOutputScriptHex)
					{
						alices.Add(alice);
					}
				}
			}

			return alices.Count > 0;
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
			return Alices.Count;
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
				if (Phase != CcjRoundPhase.InputRegistration || Status != CcjRoundStatus.Running)
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
					await Task.Delay(AliceRegistrationTimeout);

					using (await RoundSynchronizerLock.LockAsync())
					{
						// 3. If the round is still running and the phase is still InputRegistration
						if (Status == CcjRoundStatus.Running && Phase == CcjRoundPhase.InputRegistration)
						{
							Alice alice = Alices.SingleOrDefault(x => x.UniqueId == uniqueId);
							if (alice != default(Alice))
							{
								// 4. If LastSeen isn't changed by then, remove Alice.
								if (alice.LastSeen == started)
								{
									Alices.Remove(alice);
									Logger.LogInfo<CcjRound>($"Round ({RoundId}): Alice ({alice.UniqueId}) timed out.");
								}
							}
						}
					}
				});
			}
		}

		#region Modifiers

		public async Task BroadcastCoinJoinIfFullySignedAsync()
		{
			using (await RoundSynchronizerLock.LockAsync())
			{
				// Check if fully signed.
				if (SignedCoinJoin.Inputs.All(x => x.HasWitScript()))
				{
					Logger.LogInfo<CcjRound>($"Round ({RoundId}): Trying to broadcast coinjoin.");

					try
					{
						Coin[] spentCoins = Alices.SelectMany(x => x.Inputs).ToArray();
						Money networkFee = SignedCoinJoin.GetFee(spentCoins);
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Network Fee: {networkFee.ToString(false, false)} BTC.");
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Coordinator Fee: {SignedCoinJoin.Outputs.SingleOrDefault(x => x.ScriptPubKey == Constants.GetCoordinatorAddress(Network).ScriptPubKey)?.Value?.ToString(false, false) ?? "0"} BTC.");
						FeeRate feeRate = SignedCoinJoin.GetFeeRate(spentCoins);
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Network Fee Rate: {feeRate.FeePerK.ToDecimal(MoneyUnit.Satoshi) / 1000} satoshi/byte.");
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Number of inputs: {SignedCoinJoin.Inputs.Count}.");
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Number of outputs: {SignedCoinJoin.Outputs.Count}.");
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Serialized Size: {SignedCoinJoin.GetSerializedSize() / 1024} KB.");
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): VSize: {SignedCoinJoin.GetVirtualSize() / 1024} KB.");
						foreach (var o in SignedCoinJoin.GetIndistinguishableOutputs().Where(x => x.count > 1))
						{
							Logger.LogInfo<CcjRound>($"Round ({RoundId}): There are {o.count} occurences of {o.value.ToString(true, false)} BTC output.");
						}

						await RpcClient.SendRawTransactionAsync(SignedCoinJoin);
						CoinJoinBroadcasted?.Invoke(this, SignedCoinJoin);
						Succeed(syncLock: false);
						Logger.LogInfo<CcjRound>($"Round ({RoundId}): Successfully broadcasted the CoinJoin: {SignedCoinJoin.GetHash()}.");
					}
					catch (Exception ex)
					{
						Abort(nameof(CcjRound), $"Could not broadcast the CoinJoin: {SignedCoinJoin.GetHash()}.", syncLock: false);
						Logger.LogError<CcjRound>(ex);
					}
				}
			}
		}

		public void UpdateAnonymitySet(int anonymitySet)
		{
			using (RoundSynchronizerLock.Lock())
			{
				if ((Phase != CcjRoundPhase.InputRegistration) || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException($"Updating anonymity set is only allowed in {nameof(CcjRoundPhase.InputRegistration)} phase.");
				}
				AnonymitySet = anonymitySet;
			}
			Logger.LogInfo<CcjRound>($"Round ({RoundId}): {nameof(AnonymitySet)} updated: {AnonymitySet}.");
		}

		public void AddAlice(Alice alice)
		{
			using (RoundSynchronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Adding Alice is only allowed in InputRegistration phase.");
				}
				Alices.Add(alice);
			}

			StartAliceTimeout(alice.UniqueId);

			Logger.LogInfo<CcjRound>($"Round ({RoundId}): Alice ({alice.UniqueId}) added.");
		}

		public void AddBob(Bob bob)
		{
			using (RoundSynchronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.OutputRegistration || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Adding Bob is only allowed in OutputRegistration phase.");
				}
				if (Bobs.Any(x => x.ActiveOutputAddress == bob.ActiveOutputAddress))
				{
					return; // Bob is already added.
				}

				Bobs.Add(bob);
			}

			Logger.LogInfo<CcjRound>($"Round ({RoundId}): Bob added.");
		}

		public int RemoveAlicesBy(AliceState state)
		{
			int numberOfRemovedAlices = 0;
			using (RoundSynchronizerLock.Lock())
			{
				if ((Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation) || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				numberOfRemovedAlices = Alices.RemoveAll(x => x.State == state);
			}
			if (numberOfRemovedAlices != 0)
			{
				Logger.LogInfo<CcjRound>($"Round ({RoundId}): {numberOfRemovedAlices} alices in {state} state are removed.");
			}
			return numberOfRemovedAlices;
		}

		public async Task<IEnumerable<Alice>> RemoveAlicesIfAnInputRefusedByMempoolAsync()
		{
			var alicesRemoved = new List<Alice>();
			var key = new Key();

			using (RoundSynchronizerLock.Lock())
			{
				if ((Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation) || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}

				foreach (Alice alice in Alices)
				{
					foreach (Coin input in alice.Inputs)
					{
						// Check if mempool would accept a fake transaction created with the registered inputs.
						// This will catch ascendant/descendant count and size limits for example.
						var result = await RpcClient.TestMempoolAcceptAsync(input);

						if (!result.accept)
						{
							alicesRemoved.Add(alice);
							Alices.Remove(alice);
						}
					}
				}
			}

			foreach (var alice in alicesRemoved)
			{
				Logger.LogInfo<CcjRound>($"Round ({RoundId}): Alice ({alice.UniqueId}) removed.");
			}

			return alicesRemoved;
		}

		public int RemoveAlicesBy(params Guid[] ids)
		{
			var numberOfRemovedAlices = 0;
			using (RoundSynchronizerLock.Lock())
			{
				if ((Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation) || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				foreach (var id in ids)
				{
					numberOfRemovedAlices = Alices.RemoveAll(x => x.UniqueId == id);
				}
			}

			Logger.LogInfo<CcjRound>($"Round ({RoundId}): {numberOfRemovedAlices} alices are removed.");

			return numberOfRemovedAlices;
		}

		public int RemoveAliceIfContains(OutPoint input)
		{
			var numberOfRemovedAlices = 0;

			using (RoundSynchronizerLock.Lock())
			{
				if ((Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation) || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				numberOfRemovedAlices = Alices.RemoveAll(x => x.Inputs.Any(y => y.Outpoint == input));
			}

			Logger.LogInfo<CcjRound>($"Round ({RoundId}): {numberOfRemovedAlices} alices are removed.");

			return numberOfRemovedAlices;
		}

		#endregion Modifiers
	}
}
