using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class PocketCoinControlItemViewModel : CoinControlItemViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public PocketCoinControlItemViewModel(Pocket pocket)
	{
		var pocketCoins = pocket.Coins.ToList();

		var unconfirmedCount = pocketCoins.Count(x => !x.Confirmed);
		IsConfirmed = unconfirmedCount == 0;
		ConfirmationStatus = IsConfirmed ? "All coins are confirmed" : $"{unconfirmedCount} coins are waiting for confirmation";
		IsBanned = pocketCoins.Any(x => x.BannedUntilUtc is { });
		BannedUntilUtcToolTip = IsBanned ? "Some coins can't participate in coinjoin" : null;
		Amount = pocket.Amount;
		IsCoinjoining = pocketCoins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = (int)pocketCoins.Min(coin => coin.AnonymitySet);
		Labels = pocket.Labels;
		Children = pocketCoins.OrderByDescending(x => x.AnonymitySet).Select(coin => new CoinCoinControlItemViewModel(coin)).ToList();
		CanBeSelected = true;
		ScriptType = null;

		Children
			.AsObservableChangeSet()
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(c => Children.Where(x => x.SmartCoin.HdPubKey == c.Sender.SmartCoin.HdPubKey && x.IsSelected != c.Sender.IsSelected))
			.Do(coins =>
			{
				// Select/deselect all the coins on the same address.
				foreach (var coin in coins)
				{
					coin.IsSelected = !coin.IsSelected;
				}
			})
			.Select(_ =>
			{
				var totalCount = Children.Count;
				var selectedCount = Children.Count(x => x.IsSelected == true);
				return (bool?)(selectedCount == totalCount ? true : selectedCount == 0 ? false : null);
			})
			.BindTo(this, x => x.IsSelected)
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.IsSelected)
			.Do(isSelected =>
			{
				if (isSelected is null)
				{
					return;
				}

				foreach (var item in Children)
				{
					item.IsSelected = isSelected.Value;
				}
			})
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
