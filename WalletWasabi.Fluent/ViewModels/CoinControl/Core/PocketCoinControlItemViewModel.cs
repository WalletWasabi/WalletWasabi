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
		var unconfirmedCount = pocket.Coins.Count(x => !x.Confirmed);
		IsConfirmed = unconfirmedCount == 0;
		ConfirmationStatus = IsConfirmed ? "All coins are confirmed" : $"{unconfirmedCount} coins are waiting for confirmation";
		IsBanned = pocket.Coins.Any(x => x.IsBanned);
		BannedUntilUtcToolTip = IsBanned ? "Some coins can't participate in coinjoin" : null;
		Amount = pocket.Amount;
		IsCoinjoining = pocket.Coins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = (int)pocket.Coins.Max(x => x.HdPubKey.AnonymitySet);
		Labels = pocket.Labels;
		Children = pocket.Coins.OrderByDescending(x => x.Amount).Select(coin => new CoinCoinControlItemViewModel(coin)).ToList();
		CanBeSelected = true;
		ScriptType = null;

		Children
			.AsObservableChangeSet()
			.WhenPropertyChanged(x => x.IsSelected)
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
