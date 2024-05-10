using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Nito.AsyncEx;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation;

public class ThirdPartyFeeProvider : PeriodicRunner, IThirdPartyFeeProvider
{
	private int _actualFeeProviderIndex = -1;
	private bool _isPaused;

	public ThirdPartyFeeProvider(TimeSpan period, ImmutableArray<IThirdPartyFeeProvider> feeProviders)
		: base(period)
	{
		FeeProviders = feeProviders;
		if (FeeProviders.Length > 0)
		{
			ActualFeeProviderIndex = 0;
		}
	}

	public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

	public AllFeeEstimate? LastAllFeeEstimate { get; private set; }
	private object Lock { get; } = new();
	public bool InError { get; private set; }

	public bool IsPaused
	{
		get => _isPaused;
		set
		{
			_isPaused = value;
			SetPauseStates();
		}
	}

	private AbandonedTasks ProcessingEvents { get; } = new();

	private ImmutableArray<IThirdPartyFeeProvider> FeeProviders { get; }

	protected int ActualFeeProviderIndex
	{
		get => _actualFeeProviderIndex;
		set
		{
			if (_actualFeeProviderIndex != value)
			{
				_actualFeeProviderIndex = value;
				LastStatusChange = DateTimeOffset.UtcNow;
				SetPauseStates();
			}
		}
	}

	protected DateTimeOffset LastStatusChange { get; set; } = DateTimeOffset.UtcNow;

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		foreach (var feeProvider in FeeProviders)
		{
			SetAllFeeEstimateIfLooksBetter(feeProvider.LastAllFeeEstimate);
			feeProvider.AllFeeEstimateArrived += OnAllFeeEstimateArrived;
		}

		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		foreach (var feeProvider in FeeProviders)
		{
			feeProvider.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;
		}

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

			if (sender is IThirdPartyFeeProvider)
			{
				int senderIdx = FeeProviders.IndexOf((IThirdPartyFeeProvider)sender);
				if (senderIdx != -1 && senderIdx <= ActualFeeProviderIndex)
				{
					ActualFeeProviderIndex = senderIdx;
					InError = false;
					LastStatusChange = DateTimeOffset.UtcNow;

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
		}
	}

	private bool SetAllFeeEstimateIfLooksBetter(AllFeeEstimate? fees)
	{
		var current = LastAllFeeEstimate;
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
		if (LastAllFeeEstimate == fees)
		{
			return false;
		}
		LastAllFeeEstimate = fees;
		return true;
	}

	private void SetPauseStates()
	{
		for (int idx = 0; idx < FeeProviders.Length; idx++)
		{
			FeeProviders[idx].IsPaused = IsPaused || idx > _actualFeeProviderIndex;
		}
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		if (IsPaused)
		{
			return Task.CompletedTask;
		}

		bool inError = FeeProviders.Take(ActualFeeProviderIndex + 1).All(f => f.InError);

		// Let's wait a bit more
		if (inError && !InError && LastStatusChange - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
		{
			InError = true;
			ActualFeeProviderIndex = FeeProviders.Length - 1;
			LastStatusChange = DateTimeOffset.UtcNow;
		}

		return Task.CompletedTask;
	}
}
