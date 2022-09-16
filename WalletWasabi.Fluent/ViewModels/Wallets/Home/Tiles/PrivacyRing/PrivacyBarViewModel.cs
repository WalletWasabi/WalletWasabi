using Avalonia;
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
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public partial class PrivacyBarViewModel : ViewModelBase
{
	private readonly SourceList<PrivacyBarItemViewModel> _itemsSourceList = new();
	private IObservable<Unit> _coinsUpdated;

	[AutoNotify] private double _width;

	public PrivacyBarViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		Wallet = walletViewModel.Wallet;

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
			.CombineLatest(this.WhenAnyValue(x => x.Width))
			.Select(_ => walletViewModel.Wallet.GetPockets())
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(RefreshCoinsList);
	}

	public ObservableCollectionExtended<PrivacyBarItemViewModel> Items { get; } = new();

	public Wallet Wallet { get; }

	private void RefreshCoinsList(IEnumerable<Pocket> pockets)
	{
		_itemsSourceList.Edit(list =>
		{
			list.Clear();

			if (Width == 0d)
			{
				return;
			}

			var total = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));
			var start = 0.0m;

			var usableWidth = Width - (pockets.SelectMany(x => x.Coins).Count() * 2);

			foreach (var pocket in pockets.OrderByDescending(x => x.Coins.First().HdPubKey.AnonymitySet))
			{
				var pocketCoins = pocket.Coins.OrderByDescending(x => x.Amount).ToList();

				foreach (var coin in pocketCoins)
				{
					var margin = 2;
					var amount = coin.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC);
					var width = Math.Abs((decimal)usableWidth * amount / total);

					// Artificially enlarge UTXOs smaller than 2 px in order to make them visible.
					if (width < 2)
					{
						width++;
						margin--;
					}

					var item = new PrivacyBarItemViewModel(this, coin, (double)start, (double)width, Wallet);

					list.Add(item);

					start += width + margin;
				}
			}
		});
	}

	public void Dispose()
	{
		_itemsSourceList.Dispose();
	}
}
