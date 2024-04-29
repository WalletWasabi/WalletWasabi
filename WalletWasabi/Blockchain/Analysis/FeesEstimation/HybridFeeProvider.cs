using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Logging;
using WalletWasabi.Nito.AsyncEx;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation;

/// <summary>
/// Manages multiple fee sources. Returns the best one.
/// Prefers local full node, as long as the fee is accurate.
/// </summary>
public class HybridFeeProvider : IHostedService
{
	public HybridFeeProvider(IThirdPartyFeeProvider thirdPartyFeeProvider, RpcFeeProvider? rpcFeeProvider)
	{
		ThirdPartyFeeProvider = thirdPartyFeeProvider;
		RpcFeeProvider = rpcFeeProvider;
	}

	public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

	public RpcFeeProvider? RpcFeeProvider { get; }
	public IThirdPartyFeeProvider ThirdPartyFeeProvider { get; }
	private object Lock { get; } = new();
	public AllFeeEstimate? AllFeeEstimate { get; private set; }
	private AbandonedTasks ProcessingEvents { get; } = new();

	public Task StartAsync(CancellationToken cancellationToken)
	{
		SetAllFeeEstimateIfLooksBetter(RpcFeeProvider?.LastAllFeeEstimate);
		SetAllFeeEstimateIfLooksBetter(ThirdPartyFeeProvider.LastAllFeeEstimate);

		ThirdPartyFeeProvider.AllFeeEstimateArrived += OnAllFeeEstimateArrived;
		if (RpcFeeProvider is not null)
		{
			RpcFeeProvider.AllFeeEstimateArrived += OnAllFeeEstimateArrived;
		}

		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		ThirdPartyFeeProvider.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;
		if (RpcFeeProvider is not null)
		{
			RpcFeeProvider.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;
		}

		await ProcessingEvents.WhenAllAsync().ConfigureAwait(false);
	}

	private void OnAllFeeEstimateArrived(object? sender, AllFeeEstimate fees)
	{
		using (RunningTasks.RememberWith(ProcessingEvents))
		{
			// Only go further if we have estimations.
			if (fees.Estimations.Any() is not true)
			{
				return;
			}

			var notify = false;
			lock (Lock)
			{
				if (AllFeeEstimate is null)
				{
					// If it wasn't set before, then set it regardless everything.
					notify = SetAllFeeEstimate(fees);
				}
				else if (sender is IThirdPartyFeeProvider)
				{
					var rpcProvider = RpcFeeProvider;
					if (rpcProvider is null)
					{
						// If user doesn't use full node, then set it, this is the best we got.
						notify = SetAllFeeEstimate(fees);
					}
					else
					{
						if (!rpcProvider.InError)
						{
							// If user's full node is properly serving data, then we don't care about the third party.
							return;
						}

						// If the third party is properly serving accurate data then, this is the best we got.
						notify = SetAllFeeEstimate(fees);
					}
				}
				else if (sender is RpcFeeProvider rpcProvider)
				{
					// If user's full node is properly serving data, we're done here.
					notify = SetAllFeeEstimate(fees);
				}
			}

			if (notify)
			{
				var from = fees.Estimations.First();
				var to = fees.Estimations.Last();
				Logger.LogInfo($"Fee rates are acquired from {sender?.GetType()?.Name} ranging from target {from.Key} blocks at {from.Value} sat/vByte to target {to.Key} blocks at {to.Value} sat/vByte.");
				AllFeeEstimateChanged?.Invoke(this, fees);
			}
		}
	}

	/// <returns>True if changed.</returns>
	private bool SetAllFeeEstimateIfLooksBetter(AllFeeEstimate? fees)
	{
		var current = AllFeeEstimate;
		if (fees is null
			|| fees == current
			|| (current is not null && fees.Estimations.Count <= current.Estimations.Count))
		{
			return false;
		}
		return SetAllFeeEstimate(fees);
	}

	/// <returns>True if changed.</returns>
	private bool SetAllFeeEstimate(AllFeeEstimate fees)
	{
		if (AllFeeEstimate == fees)
		{
			return false;
		}
		AllFeeEstimate = fees;
		return true;
	}
}
