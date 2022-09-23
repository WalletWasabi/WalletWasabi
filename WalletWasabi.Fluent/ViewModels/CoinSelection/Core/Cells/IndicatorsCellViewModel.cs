using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class IndicatorsCellViewModel : ViewModelBase
{
	public IndicatorsCellViewModel(ICoin coin)
	{
		IsCoinjoining = coin.WhenAnyValue(x => x.IsCoinjoining);
		IsConfirmed = coin.WhenAnyValue(x => x.IsConfirmed);
		IsBanned = coin.WhenAnyValue(x => x.BannedUntil, offset => offset.HasValue);
		ConfirmationStatus = coin.WhenAnyValue(x => x.IsConfirmed).Select(isConfirmed => isConfirmed ? "Confirmed" : "Pending confirmation").ReplayLastActive();
		BannedUntilUtcToolTip = coin.WhenAnyValue(x => x.BannedUntil).Select(x => x.HasValue ? $"Can't participate in coinjoin until: {x:g}" : "").ReplayLastActive();
	}

	public IObservable<bool> IsBanned { get; set; }

	public IObservable<string> BannedUntilUtcToolTip { get; }

	public IObservable<bool> IsCoinjoining { get; }

	public IObservable<bool> IsConfirmed { get; }

	public IObservable<string?> ConfirmationStatus { get; }
}
