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

namespace WalletWasabi.ChaumianCoinJoin
{
	public class CcjRound
	{
		public static long RoundCount;

		public RPCClient RpcClient { get; }

		public Money Denomination { get; }
		public int ConfirmationTarget { get; }
		public Money CoordinatorFee { get; }
		public int AnonymitySet { get; private set; }

		public Money FeePerInputs { get; private set; }
		public Money FeePerOutputs { get; private set; }

		public string RoundHash { get; private set; }

		public Transaction UnsignedCoinJoin { get; private set; }
		public Transaction SignedCoinJoin { get; private set; }

		private List<Alice> Alices { get; }
		private AsyncLock AlicesLock { get; }
		private List<Bob> Bobs { get; }
		private AsyncLock BobsLock { get; }

		public CcjRoundPhase Phase { get; private set; }
		private static AsyncLock PhaseExecutionLock { get; } = new AsyncLock();

		public CcjRoundStatus Status { get; private set; }

		public CcjRound(RPCClient rpc, CcjRoundConfig config)
		{
			Interlocked.Increment(ref RoundCount);

			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			Guard.NotNull(nameof(config), config);

			Denomination = config.Denomination;
			ConfirmationTarget = (int)config.ConfirmationTarget;
			CoordinatorFee = new Money(((decimal)config.CoordinatorFeePercent * 0.01m) * decimal.Parse(Denomination.ToString(false, true)), MoneyUnit.BTC);
			AnonymitySet = (int)config.AnonymitySet;

			Phase = CcjRoundPhase.InputRegistration;
			Status = CcjRoundStatus.NotStarted;

			RoundHash = null;

			UnsignedCoinJoin = null;
			SignedCoinJoin = null;

			Alices = new List<Alice>();
			AlicesLock = new AsyncLock();
			Bobs = new List<Bob>();
			BobsLock = new AsyncLock();
		}

		/// <returns>The next phase or null, if the round is not running.</returns>
		public async Task<CcjRoundPhase?> ExecuteNextPhaseAsync()
		{
			using (await PhaseExecutionLock.LockAsync())
			using (await AlicesLock.LockAsync())
			using (await BobsLock.LockAsync())
			{
				try
				{
					if (Status == CcjRoundStatus.NotStarted) // So start the input registration phase
					{
						// Calculate fees
						var inputSizeInBytes = (int)Math.Ceiling(((3 * Constants.P2wpkhInputSizeInBytes) + Constants.P2pkhInputSizeInBytes) / 4m);
						var outputSizeInBytes = Constants.OutputSizeInBytes;
						try
						{
							var estimateSmartFeeResponse = await RpcClient.EstimateSmartFeeAsync(ConfirmationTarget, EstimateSmartFeeMode.Conservative);
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
						return CcjRoundPhase.InputRegistration;
					}
					else if (Status != CcjRoundStatus.Running) // Failed or succeeded, swallow
					{
						return null;
					}
					else if (Phase == CcjRoundPhase.InputRegistration)
					{
						RoundHash = NBitcoinHelpers.HashOutpoints(Alices.SelectMany(x => x.Inputs).Select(y => y.Key));

						Phase = CcjRoundPhase.ConnectionConfirmation;
						return CcjRoundPhase.ConnectionConfirmation;
					}
					else if (Phase == CcjRoundPhase.ConnectionConfirmation)
					{
						Phase = CcjRoundPhase.OutputRegistration;
						return CcjRoundPhase.OutputRegistration;
					}
					else if (Phase == CcjRoundPhase.OutputRegistration)
					{
						// Build CoinJoin

						// 1. Set new denomination: minor optimization.
						Money newDenomination = Alices.Min(x => x.OutputSum);
						var transaction = new Transaction();

						// 2. Add Bob outputs.
						foreach (Bob bob in Bobs)
						{
							transaction.AddOutput(newDenomination, bob.ActiveOutputScript);
						}

						// 3. If there are less Bobs than Alices, then add our own address. The malicious Alice, who will refuse to sign.
						for (int i = 0; i < Alices.Count - Bobs.Count; i++)
						{
							var donation = Constants.GetFailedZeroLinkDosAttackAddress(RpcClient.Network);
							transaction.AddOutput(Denomination, donation);
						}

						// 4. Add the inputs and the changes of Alices.
						foreach (Alice alice in Alices)
						{
							foreach (var input in alice.Inputs)
							{
								transaction.AddInput(new TxIn(input.Key));
							}
							transaction.AddOutput(alice.GetChangeAmount(newDenomination), alice.ChangeOutputScript);
						}

						// 5. Create the unsigned transaction.
						var builder = new TransactionBuilder();
						UnsignedCoinJoin = builder
							.ContinueToBuild(transaction)
							.Shuffle()
							.BuildTransaction(false);

						Phase = CcjRoundPhase.Signing;
						return CcjRoundPhase.Signing;
					}
					else
					{
						throw new InvalidOperationException("Last phase is reached.");
					}
				}
				catch (Exception ex)
				{
					Logger.LogError<CcjRound>(ex);
					Status = CcjRoundStatus.Failed;
					throw;
				}
			}
		}

		public void Fail()
		{
			using (PhaseExecutionLock.Lock())
			{
				Status = CcjRoundStatus.Failed;
			}
		}
	}
}
