using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public class PocketViewModel : CoinListItem
{
	public PocketViewModel(Pocket pocket, ICoinListModel availableCoins, bool canSelectCoinjoiningCoins, bool ignorePrivacyMode)
	{
		var pocketCoins = pocket.Coins.ToList();

		var unconfirmedCount = pocketCoins.Count(x => !x.Confirmed);
		IsConfirmed = unconfirmedCount == 0;
		IgnorePrivacyMode = ignorePrivacyMode;
		ConfirmationStatus = IsConfirmed ? "All coins are confirmed" : $"{unconfirmedCount} coins are waiting for confirmation";
		IsBanned = pocketCoins.Any(x => x.IsBanned);
		BannedUntilUtcToolTip = IsBanned ? "Some coins can't participate in coinjoin" : null;
		Amount = new Amount(pocket.Amount);
		IsCoinjoining = pocketCoins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = GetAnonScore(pocketCoins);
		Labels = pocket.Labels;
		Children =
			pocketCoins
				.Select(availableCoins.GetCoinModel)
				.OrderByDescending(x => x.AnonScore)
				.Select(coin => new CoinViewModel("", coin, canSelectCoinjoiningCoins, ignorePrivacyMode) { IsChild = true })
				.ToList();

		Children
			.AsObservableChangeSet()
			.AutoRefresh(x => IsExcludedFromCoinJoin)
			.Select(_ => Children.All(x => x.IsExcludedFromCoinJoin))
			.BindTo(this, x => x.IsExcludedFromCoinJoin)
			.DisposeWith(_disposables);

		Children
			.AsObservableChangeSet()
			.AutoRefresh(x => IsCoinjoining)
			.Select(_ => Children.Any(x => x.IsCoinjoining))
			.BindTo(this, x => x.IsCoinjoining)
			.DisposeWith(_disposables);

		ScriptType = null;

		Children
			.AsObservableChangeSet()
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(c => Children.Where(x => x.Coin.IsSameAddress(c.Sender.Coin) && x.IsSelected != c.Sender.IsSelected))
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

		ThereAreSelectableCoins()
			.BindTo(this, x => x.CanBeSelected)
			.DisposeWith(_disposables);
	}

	private IObservable<bool> ThereAreSelectableCoins() => Children
		.AsObservableChangeSet()
		.AutoRefresh(x => x.CanBeSelected)
		.Filter(x => x.CanBeSelected)
		.Count()
		.Select(i => i > 0);

	private static int? GetAnonScore(IEnumerable<SmartCoin> pocketCoins)
	{
		var allScores = pocketCoins.Select(x => (int?)x.AnonymitySet);
		return CommonOrDefault(allScores.ToList());
	}

	/// <summary>
	/// Returns the common item in the list, if any.
	/// </summary>
	/// <typeparam name="T">Type of the item</typeparam>
	/// <param name="list">List of items to determine the common item.</param>
	/// <returns>The common item or <c>default</c> if there is no common item.</returns>
	private static T? CommonOrDefault<T>(IList<T> list)
	{
		var commonItem = list[0];

		for (var i = 1; i < list.Count; i++)
		{
			if (!Equals(list[i], commonItem))
			{
				return default;
			}
		}

		return commonItem;
	}

	public override string Key => Labels.ToString();
}
