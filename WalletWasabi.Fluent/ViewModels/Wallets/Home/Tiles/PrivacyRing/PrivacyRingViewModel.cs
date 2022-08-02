using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

[NavigationMetaData(
	Title = "Wallet Coins",
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class PrivacyRingViewModel : RoutableViewModel
{
	private readonly SourceList<PrivacyRingItemViewModel> _itemsSourceList = new();
	private IObservable<Unit> _coinsUpdated;
	[AutoNotify] private PrivacyRingItemViewModel? _selectedItem;

	public PrivacyRingViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		Wallet = walletViewModel.Wallet;

		OuterRadius = 250d;
		InnerRadius = 240d;

		NextCommand = CancelCommand;

		PreviewItems.Add(walletViewModel.Tiles.OfType<PrivacyControlTileViewModel>().FirstOrDefault());

		_itemsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(Items)
			.DisposeMany()
			.Subscribe();

		_coinsUpdated =
			balanceChanged.ToSignal()
						  .Merge(walletViewModel
						  .WhenAnyValue(w => w.IsCoinJoining)
						  .ToSignal());

		_coinsUpdated
			.Select(_ => walletViewModel.Wallet.GetPockets())
			.Subscribe(RefreshCoinsList);
	}

	public ObservableCollectionExtended<PrivacyRingItemViewModel> Items { get; } = new();
	public ObservableCollectionExtended<IPrivacyRingPreviewItem> PreviewItems { get; } = new();

	public double OuterRadius { get; }
	public double InnerRadius { get; }

	public Wallet Wallet { get; }

	private void RefreshCoinsList(IEnumerable<Pocket> pockets)
	{
		_itemsSourceList.Edit(list =>
		{
			list.Clear();

			var total = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));
			var start = 0.0m;

			foreach (var pocket in pockets.OrderByDescending(x => x.Coins.First().HdPubKey.AnonymitySet))
			{
				var pocketCoins = pocket.Coins.OrderByDescending(x => x.Amount).ToList();

				foreach (var coin in pocketCoins)
				{
					var end = start + (Math.Abs(coin.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)) / total);

					var item = new PrivacyRingItemViewModel(this, coin, (double)start, (double)end);

					list.Add(item);

					start = end;
				}
			}

			PreviewItems.RemoveRange(1, PreviewItems.Count - 1);
			PreviewItems.AddRange(list);
		});
	}

	public void Dispose()
	{
		foreach (var item in Items)
		{
			item.Dispose();
		}

		_itemsSourceList.Dispose();
	}
}
