using System;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Extensions;
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
		// Upstream now exposes the current snapshot synchronously; absence of a
		// snapshot disables the cards instead of waiting on the removed async API.
		// Continue listening so unavailable estimates recover without reopening Send.
		var quotes = Observable.Defer(() => UiContext.Services.EventBus.AsObservable<MiningFeeRatesChanged>()
			.Select(change => change.AllFeeEstimate)
			.StartWith(_wallet.FeeRateEstimations))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(snapshot => TransactionFeeHelper.TryGetFeeEstimates(snapshot, _wallet.Network, out var estimates)
				? CreateMobileQuotes(chart, estimates)
				: Array.Empty<MobileFeeTargetQuote>());

		return new MobileSendFeeSelection(UiContext.Services.GetFeeTarget(), UiContext.Services.SetFeeTarget,
			quotes, RxApp.MainThreadScheduler);
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
