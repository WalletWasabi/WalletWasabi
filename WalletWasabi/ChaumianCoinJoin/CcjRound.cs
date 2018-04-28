using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.ChaumianCoinJoin
{
	public class CcjRound
	{
		public static long RoundCount;

		public RPCClient RpcClient { get; }

		public Money Denomination { get; }
		public int ConfirmationTarget { get; }
		public decimal CoordinatorFeePercent { get; }
		public int AnonymitySet { get; private set; }

		public Money FeePerInputs { get; private set; }
		public Money FeePerOutputs { get; private set; }

		public string RoundHash { get; private set; }

		public Transaction UnsignedCoinJoin { get; private set; }
		public Transaction SignedCoinJoin { get; private set; }

		private List<Alice> Alices { get; }
		private List<Bob> Bobs { get; }

		private static AsyncLock RoundSyncronizerLock { get; } = new AsyncLock();

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
				lock (PhaseLock)
				{
					if (_phase != value)
					{
						_phase = value;
						OnPhaseChanged(value);
					}
				}
			}
		}

		public event EventHandler<CcjRoundPhase> PhaseChanged;
		private void OnPhaseChanged(CcjRoundPhase phase) => PhaseChanged?.Invoke(this, phase);

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
				lock (StatusLock)
				{
					if (_status != value)
					{
						_status = value;
						OnStatusChanged(value);
					}
				}
			}
		}

		public event EventHandler<CcjRoundStatus> StatusChanged;
		private void OnStatusChanged(CcjRoundStatus status) => StatusChanged?.Invoke(this, status);

		public TimeSpan AliceRegistrationTimeout => ConnectionConfirmationTimeout;

		public TimeSpan InputRegistrationTimeout { get; }

		public TimeSpan ConnectionConfirmationTimeout { get; }

		public TimeSpan OutputRegistrationTimeout { get; }

		public TimeSpan SigningTimeout { get; }

		public UtxoReferee UtxoReferee { get; }

		public CcjRound(RPCClient rpc, UtxoReferee utxoReferee, CcjRoundConfig config)
		{
			Interlocked.Increment(ref RoundCount);

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

			RoundHash = null;

			UnsignedCoinJoin = null;
			SignedCoinJoin = null;

			Alices = new List<Alice>();
			Bobs = new List<Bob>();
		}

		public async Task ExecuteNextPhaseAsync(CcjRoundPhase expectedPhase)
		{
			using (await RoundSyncronizerLock.LockAsync())
			{
				try
				{
					if (Status == CcjRoundStatus.NotStarted) // So start the input registration phase
					{
						if(expectedPhase != CcjRoundPhase.InputRegistration)
						{
							return;
						}

						// Calculate fees
						var inputSizeInBytes = (int)Math.Ceiling(((3 * Constants.P2wpkhInputSizeInBytes) + Constants.P2pkhInputSizeInBytes) / 4m);
						var outputSizeInBytes = Constants.OutputSizeInBytes;
						try
						{
							var estimateSmartFeeResponse = await RpcClient.EstimateSmartFeeAsync(ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true);
							if (estimateSmartFeeResponse == null) throw new InvalidOperationException("FeeRate is not yet initialized");
							var feeRate = estimateSmartFeeResponse.FeeRate;
							Money feePerBytes = (feeRate.FeePerK / 1000);

							// Make sure min relay fee (1000 sat) is hit.
							FeePerInputs = Math.Max(feePerBytes * inputSizeInBytes, new Money(500));
							FeePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, new Money(250));
						}
						catch (Exception ex)
						{
							// If fee hasn't been initialized once, fall back.
							if (FeePerInputs == null || FeePerOutputs == null)
							{
								var feePerBytes = new Money(100); // 100 satoshi per byte

								// Make sure min relay fee (1000 sat) is hit.
								FeePerInputs = Math.Max(feePerBytes * inputSizeInBytes, new Money(500));
								FeePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, new Money(250));
							}

							Logger.LogError<CcjRound>(ex);
						}

						Status = CcjRoundStatus.Running;
					}
					else if (Status != CcjRoundStatus.Running) // Failed or succeeded, swallow
					{
						return;
					}
					else if (Phase == CcjRoundPhase.InputRegistration)
					{
						if (expectedPhase != CcjRoundPhase.ConnectionConfirmation)
						{
							return;
						}

						RoundHash = NBitcoinHelpers.HashOutpoints(Alices.SelectMany(x => x.Inputs).Select(y => y.OutPoint));

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
						var transaction = new Transaction();

						// 2. Add Bob outputs.
						foreach (Bob bob in Bobs)
						{
							transaction.AddOutput(newDenomination, bob.ActiveOutputScript);
						}

						BitcoinWitPubKeyAddress coordinatorAddress = Constants.GetCoordinatorAddress(RpcClient.Network);
						// 3. If there are less Bobs than Alices, then add our own address. The malicious Alice, who will refuse to sign.
						for (int i = 0; i < Alices.Count - Bobs.Count; i++)
						{
							transaction.AddOutput(newDenomination, coordinatorAddress);
						}

						// 4. Start building Coordinator fee.
						Money coordinatorFeePerAlice = new Money((CoordinatorFeePercent * 0.01m) * decimal.Parse(newDenomination.ToString(false, true)), MoneyUnit.BTC);
						Money coordinatorFee = Alices.Count * coordinatorFeePerAlice;

						// 5. Add the inputs and the changes of Alices.
						foreach (Alice alice in Alices)
						{
							foreach (var input in alice.Inputs)
							{
								transaction.AddInput(new TxIn(input.OutPoint));
							}
							Money changeAmount = alice.GetChangeAmount(newDenomination, coordinatorFeePerAlice);
							if (changeAmount > Money.Zero) // If the coordinator fee would make change amount to be negative or zero then no need to pay it.
							{
								Money minimumOutputAmount = new Money(0.0001m, MoneyUnit.BTC); // If the change would be less than about $1 then add it to the coordinator.
								Money onePercentOfDenomination = new Money(newDenomination.ToDecimal(MoneyUnit.BTC) * 0.01m, MoneyUnit.BTC); // If the change is less than about 1% of the denomination then add it to the coordinator fee.
								Money minimumChangeAmount = Math.Max(minimumOutputAmount, onePercentOfDenomination);
								if (changeAmount < minimumChangeAmount)
								{
									coordinatorFee += changeAmount;
								}
								else
								{
									transaction.AddOutput(changeAmount, alice.ChangeOutputScript);
								}
							}
						}

						// 6. Add Coordinator fee.
						transaction.AddOutput(coordinatorFee, coordinatorAddress);

						// 7. Create the unsigned transaction.
						var builder = new TransactionBuilder();
						UnsignedCoinJoin = builder
							.ContinueToBuild(transaction)
							.Shuffle()
							.BuildTransaction(false);

						Phase = CcjRoundPhase.Signing;
					}
					else
					{
						return;
					}
				}
				catch (Exception ex)
				{
					Logger.LogError<CcjRound>(ex);
					Status = CcjRoundStatus.Failed;
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

				// Delay asyncronously to the requested timeout.
				await Task.Delay(timeout);

				var executeRunFailure = false;
				using (await RoundSyncronizerLock.LockAsync())
				{
					executeRunFailure = Status == CcjRoundStatus.Running && Phase == expectedPhase;
				}
				if (executeRunFailure)
				{
					// This will happen outside the lock.
					Task.Run(async () =>
					{
						switch (expectedPhase)
						{
							case CcjRoundPhase.InputRegistration:
								{
									// Only fail if less two one Alice is registered.
									// Don't ban anyone, it's ok if they lost connection.
									var alicesToBan = await RemoveAlicesIfInputsSpentAsync();
									if (alicesToBan.Count() != 0)
									{
										await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.Now, alicesToBan.SelectMany(x => x.Inputs).Select(y => y.OutPoint).ToArray());
									}
									int aliceCountAfterInputRegistrationTimeout = CountAlices();
									if (aliceCountAfterInputRegistrationTimeout < 2)
									{
										Fail();
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
									// Only fail if less than two one alices are registered.
									// Don't ban anyone, it's ok if they lost connection.
									RemoveAlicesBy(AliceState.InputsRegistered);
									var alicesToBan = await RemoveAlicesIfInputsSpentAsync();
									if (alicesToBan.Count() != 0)
									{
										await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.Now, alicesToBan.SelectMany(x => x.Inputs).Select(y => y.OutPoint).ToArray());
									}
									int aliceCountAfterConnectionConfirmationTimeout = CountAlices();
									if (aliceCountAfterConnectionConfirmationTimeout < 2)
									{
										Fail();
									}
									else
									{
										UpdateAnonymitySet(aliceCountAfterConnectionConfirmationTimeout);
										// Progress to the next phase, which will be OutputRegistration
										await ExecuteNextPhaseAsync(CcjRoundPhase.OutputRegistration);
									}
								}
								break;
							case CcjRoundPhase.OutputRegistration:
								{
									// Output registration never fails.
									// We don't know which Alice to ban.
									// Therefore proceed to signing, and whichever Alice doesn't sign ban.
									await ExecuteNextPhaseAsync(CcjRoundPhase.Signing);
								}
								break;
							case CcjRoundPhase.Signing:
								{
									var alicesToBan = await RemoveAlicesIfInputsSpentAsync();
									if (alicesToBan.Count() != 0)
									{
										await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.Now, alicesToBan.SelectMany(x => x.Inputs).Select(y => y.OutPoint).ToArray());
									}
									Fail();
								}
								break;
							default: throw new InvalidOperationException("This is impossible to happen.");
						}
					});
				}
			});
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}

		public void Fail()
		{
			using (RoundSyncronizerLock.Lock())
			{
				Status = CcjRoundStatus.Failed;
			}
		}

		public int CountAlices(bool syncronized = true)
		{
			if (syncronized)
			{
				using (RoundSyncronizerLock.Lock())
				{
					return Alices.Count;
				}
			}
			else
			{
				return Alices.Count;
			}
		}

		public bool ContainsBlindedOutput(string blindedOutputHex, out List<Alice> alices)
		{
			alices = new List<Alice>();

			using (RoundSyncronizerLock.Lock())
			{
				foreach (Alice alice in Alices)
				{
					if (alice.BlindedOutputHex == blindedOutputHex)
					{
						alices.Add(alice);
					}
				}
			}

			if (alices.Count > 0)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool ContainsInput(OutPoint input, out List<Alice> alices)
		{
			alices = new List<Alice>();

			using (RoundSyncronizerLock.Lock())
			{
				foreach (Alice alice in Alices)
				{
					if (alice.Inputs.Any(x => x.OutPoint == input))
					{
						alices.Add(alice);
					}
				}
			}

			return alices.Count > 0;
		}

		public int CountBobs(bool syncronized = true)
		{
			if (syncronized)
			{
				using (RoundSyncronizerLock.Lock())
				{
					return Bobs.Count;
				}
			}
			else
			{
				return Alices.Count;
			}
		}

		public IEnumerable<Alice> GetAlicesBy(AliceState state)
		{
			using (RoundSyncronizerLock.Lock())
			{
				return Alices.Where(x => x.State == state).ToList();
			}
		}

		public IEnumerable<Alice> GetAlicesByNot(AliceState state)
		{
			using (RoundSyncronizerLock.Lock())
			{
				return Alices.Where(x => x.State != state).ToList();
			}
		}

		public void StartAliceTimeout(Guid uniqueId)
		{
			// 1. Find Alice and set its LastSeen propery.
			var foundAlice = false;
			var started = DateTimeOffset.UtcNow;
			using (RoundSyncronizerLock.Lock())
			{
				if(Phase != CcjRoundPhase.InputRegistration || Status != CcjRoundStatus.Running)
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
					// 2. Delay asyncronously to the requested timeout
					await Task.Delay(AliceRegistrationTimeout);

					using (await RoundSyncronizerLock.LockAsync())
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
								}
							}
						}
					}
				});
			}
		}

		#region Modifiers

		public void UpdateAnonymitySet(int anonymitySet)
		{
			using (RoundSyncronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Updating anonymity set is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				AnonymitySet = anonymitySet;
			}
		}

		public void AddAlice(Alice alice)
		{
			using (RoundSyncronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Adding Alice is only allowed in InputRegistration phase.");
				}
				Alices.Add(alice);
			}

			StartAliceTimeout(alice.UniqueId);
		}

		public void RemoveAlicesBy(AliceState state)
		{
			using (RoundSyncronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				Alices.RemoveAll(x => x.State == state);
			}
		}

		public async Task<IEnumerable<Alice>> RemoveAlicesIfInputsSpentAsync()
		{
			using (RoundSyncronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}

				var alicesRemoved = new List<Alice>();
				foreach (Alice alice in Alices)
				{
					foreach (OutPoint input in alice.Inputs.Select(y => y.OutPoint))
					{
						GetTxOutResponse getTxOutResponse = await RpcClient.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true);

						// Check if inputs are unspent.				
						if (getTxOutResponse == null)
						{
							alicesRemoved.Add(alice);
							Alices.Remove(alice);
						}
					}
				}
				return alicesRemoved;
			}
		}

		public void RemoveAlicesBy(Guid id)
		{
			using (RoundSyncronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				Alices.RemoveAll(x => x.UniqueId == id);
			}
		}

		public void RemoveAliceIfContains(OutPoint input)
		{
			using (RoundSyncronizerLock.Lock())
			{
				if (Phase != CcjRoundPhase.InputRegistration && Phase != CcjRoundPhase.ConnectionConfirmation || Status != CcjRoundStatus.Running)
				{
					throw new InvalidOperationException("Removing Alice is only allowed in InputRegistration and ConnectionConfirmation phases.");
				}
				Alices.RemoveAll(x => x.Inputs.Any(y => y.OutPoint == input));
			}
		}

		#endregion
	}
}
