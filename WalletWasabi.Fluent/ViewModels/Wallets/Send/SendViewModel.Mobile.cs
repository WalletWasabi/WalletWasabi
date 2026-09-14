using System;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class SendViewModel
{
	/// <summary>Creates a view-owned, cancellable projection of the wallet's fee estimates.</summary>
	public MobileSendFeeSelection CreateMobileFeeSelection()
	{
		var chart = new FeeChartViewModel(UiContext);
		var updates = UiContext.Services.EventBus.AsObservable<MiningFeeRatesChanged>()
			.Select(e =>
			{
				TransactionFeeHelper.TryGetFeeEstimates(e.AllFeeEstimate, _wallet.Network, out var estimates);
				return estimates;
			})
			.Where(estimates => estimates is not null && estimates.Estimations.Count > 0)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(estimates => CreateMobileQuotes(chart, estimates!))
			.Publish().RefCount();

		var initial = Observable.FromAsync(cancellation => TransactionFeeHelper.GetFeeEstimatesWhenReadyAsync(_wallet, cancellation))
			.Timeout(TimeSpan.FromSeconds(15))
			.TakeUntil(updates)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(estimates => CreateMobileQuotes(chart, estimates))
			.Catch<MobileFeeTargetQuote[], Exception>(_ => Observable.Return(Array.Empty<MobileFeeTargetQuote>()));

		return new MobileSendFeeSelection(UiContext.Services.GetFeeTarget(), UiContext.Services.SetFeeTarget,
			initial.Merge(updates), RxApp.MainThreadScheduler);
	}

	private static MobileFeeTargetQuote[] CreateMobileQuotes(FeeChartViewModel chart, FeeRateEstimations estimates)
	{
		var values = estimates.WildEstimations.ToArray();
		if (values.Length == 0) return Array.Empty<MobileFeeTargetQuote>();
		chart.UpdateFeeEstimates(values);
		if (chart.ConfirmationTargetValues is not { Length: > 0 } targets) return Array.Empty<MobileFeeTargetQuote>();
		var minimum = Math.Max(1, (int)Math.Ceiling(targets.Min()));
		var maximum = Math.Max(minimum, (int)Math.Ceiling(targets.Max()));
		return new[] { 6, 3, 1 }.Select(requested =>
		{
			var target = Math.Clamp(requested, minimum, maximum);
			return new MobileFeeTargetQuote(requested, target, chart.GetSatoshiPerByte(target));
		}).ToArray();
	}
}
