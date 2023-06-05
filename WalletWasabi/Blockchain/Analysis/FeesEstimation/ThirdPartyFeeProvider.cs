using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.Services;
using WalletWasabi.WebClients.BlockstreamInfo;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation;

public class ThirdPartyFeeProvider : PeriodicRunner, IThirdPartyFeeProvider
{
	public ThirdPartyFeeProvider(TimeSpan period, WasabiSynchronizer synchronizer, BlockstreamInfoFeeProvider blockstreamProvider)
		: base(period)
	{
		Synchronizer = synchronizer;
		BlockstreamProvider = blockstreamProvider;
	}

	public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

	public WasabiSynchronizer Synchronizer { get; }
	public BlockstreamInfoFeeProvider BlockstreamProvider { get; }
	public AllFeeEstimate? LastAllFeeEstimate { get; private set; }
	private object Lock { get; } = new();
	public bool InError { get; private set; }
	private AbandonedTasks ProcessingEvents { get; } = new();

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		SetAllFeeEstimateIfLooksBetter(Synchronizer.LastAllFeeEstimate);
		SetAllFeeEstimateIfLooksBetter(BlockstreamProvider.LastAllFeeEstimate);

		Synchronizer.AllFeeEstimateArrived += OnAllFeeEstimateArrived;
		BlockstreamProvider.AllFeeEstimateArrived += OnAllFeeEstimateArrived;

		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		Synchronizer.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;
		BlockstreamProvider.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;

		await ProcessingEvents.WhenAllAsync().ConfigureAwait(false);
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	private void OnAllFeeEstimateArrived(object? sender, AllFeeEstimate? fees)
	{
		using (RunningTasks.RememberWith(ProcessingEvents))
		{
			// Only go further if we have estimations.
			if (fees?.Estimations?.Any() is not true)
			{
				return;
			}

			var notify = false;
			lock (Lock)
			{
				notify = SetAllFeeEstimate(fees);
			}

			if (notify)
			{
				AllFeeEstimateArrived?.Invoke(sender, fees);
			}
		}
	}

	private bool SetAllFeeEstimateIfLooksBetter(AllFeeEstimate? fees)
	{
		var current = LastAllFeeEstimate;
		if (fees is null
			|| fees == current
			|| (current is not null && ((!fees.IsAccurate && current.IsAccurate) || fees.Estimations.Count <= current.Estimations.Count)))
		{
			return false;
		}
		return SetAllFeeEstimate(fees);
	}

	/// <returns>True if changed.</returns>
	private bool SetAllFeeEstimate(AllFeeEstimate fees)
	{
		if (LastAllFeeEstimate == fees)
		{
			return false;
		}
		LastAllFeeEstimate = fees;
		return true;
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		// If the backend doesn't work for a period of time, then and only then start using Blockstream.
		if (Synchronizer.InError && Synchronizer.BackendStatusChangedSince > TimeSpan.FromMinutes(1))
		{
			BlockstreamProvider.IsPaused = false;
			InError = BlockstreamProvider.InError;
		}
		else
		{
			BlockstreamProvider.IsPaused = true;
			InError = false;
		}

		return Task.CompletedTask;
	}
}
