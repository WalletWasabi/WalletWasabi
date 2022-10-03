using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class IndicatorsCellViewModel : ViewModelBase
{
	public IndicatorsCellViewModel(ICoin coin)
	{
		Coin = coin;

		IsVisible = this.WhenAnyValue(x => x.Coin.OutPoint, x => x != OutPoint.Zero);

		IsCoinjoining = this.WhenAnyValue(x => x.Coin.IsCoinjoining);
		IsConfirmed = this.WhenAnyValue(x => x.Coin.IsConfirmed);
		IsBanned = this.WhenAnyValue(x => x.Coin.BannedUntil, x => x.HasValue);
		ConfirmationStatus = this.WhenAnyValue(x => x.Coin.IsConfirmed, isConfirmed => isConfirmed ? "Confirmed" : "Pending confirmation");
		BannedUntilUtcToolTip = this.WhenAnyValue(x => x.Coin.BannedUntil, x => x.HasValue ? $"Can't participate in coinjoin until: {x:g}" : "");
	}

	public IObservable<bool> IsVisible { get; }

	public IObservable<bool> IsBanned { get; }

	public IObservable<string> BannedUntilUtcToolTip { get; }

	public IObservable<bool> IsCoinjoining { get; }

	public IObservable<bool> IsConfirmed { get; }

	public IObservable<string?> ConfirmationStatus { get; }

	private ICoin Coin { get; }
}
