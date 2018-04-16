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

		private List<Alice> Alices { get; }
		private AsyncLock AlicesLock { get; }
		private List<Bob> Bobs { get; }
		private AsyncLock BobsLock { get; }

		public CcjRoundPhase Phase { get; private set; }
		private static AsyncLock PhaseExecutionLock { get; } = new AsyncLock();

		public CcjRoundStatus Status { get; private set; }

		public CcjRound(RPCClient rpc, Money denomination, int confirmationTarget, decimal coordinatorFeePercent, int anonymitySet)
		{
			Interlocked.Increment(ref RoundCount);

			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			ConfirmationTarget = confirmationTarget;
			CoordinatorFee = new Money((coordinatorFeePercent * 0.01m) * decimal.Parse(Denomination.ToString(false, true)), MoneyUnit.BTC);
			AnonymitySet = anonymitySet;

			Phase = CcjRoundPhase.InputRegistration;
			Status = CcjRoundStatus.NotStarted;

			RoundHash = null;

			Alices = new List<Alice>();
			AlicesLock = new AsyncLock();
			Bobs = new List<Bob>();
			BobsLock = new AsyncLock();
		}

		public async Task ExecuteNextPhaseAsync()
		{
			using (await PhaseExecutionLock.LockAsync())
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
					}
					else if (Status != CcjRoundStatus.Running) // Failed or succeeded, swallow
					{
						return;
					}
					else if (Phase == CcjRoundPhase.InputRegistration)
					{
						RoundHash = NBitcoinHelpers.HashOutpoints(Alices.SelectMany(x => x.Inputs).Select(y => y.Key));

						Phase = CcjRoundPhase.ConnectionConfirmation;
					}
					else if (Phase == CcjRoundPhase.ConnectionConfirmation)
					{
						Phase = CcjRoundPhase.OutputRegistration;
					}
					else if (Phase == CcjRoundPhase.OutputRegistration)
					{
						// Build CoinJoin

						Phase = CcjRoundPhase.Signing;
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
