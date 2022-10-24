using Avalonia;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
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
	private readonly CompositeDisposable _disposables = new();
	private readonly SourceList<PrivacyRingItemViewModel> _itemsSourceList = new();
	private IObservable<Unit> _coinsUpdated;
	[AutoNotify] private PrivacyRingItemViewModel? _selectedItem;
	[AutoNotify] private double _height;
	[AutoNotify] private double _width;
	[AutoNotify] private Thickness _margin;
	[AutoNotify] private Thickness _negativeMargin;

	public PrivacyRingViewModel(WalletViewModel walletViewModel)
	{
		Wallet = walletViewModel.Wallet;

		NextCommand = CancelCommand;
		PrivacyTile = new PrivacyControlTileViewModel(walletViewModel, false);
		PrivacyTile.Activate(_disposables);

		PreviewItems.Add(PrivacyTile);

		_itemsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(Items)
			.DisposeMany()
			.Subscribe();

		_coinsUpdated =
			walletViewModel.UiTriggers.PrivacyProgressUpdateTrigger
						  .Merge(walletViewModel
						  .WhenAnyValue(w => w.IsCoinJoining)
						  .ToSignal());

		_coinsUpdated
			.Select(_ => walletViewModel.Wallet.GetPockets())
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(RefreshCoinsList);

		_disposables.Add(_itemsSourceList);

		this.WhenAnyValue(x => x.Width, x => x.Height)
			.Subscribe(x =>
			{
				var usableHeight = x.Item2;
				var usableWidth = x.Item1;
				Margin = new Thickness(usableWidth / 2, usableHeight / 2, 0, 0);
				NegativeMargin = new Thickness(Margin.Left * -1, Margin.Top * -1, 0, 0);
				RefreshCoinsList(walletViewModel.Wallet.GetPockets());
			});

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public PrivacyControlTileViewModel PrivacyTile { get; }

	public ObservableCollectionExtended<PrivacyRingItemViewModel> Items { get; } = new();
	public ObservableCollectionExtended<PrivacyRingItemViewModel> References { get; } = new();
	public ObservableCollectionExtended<IPrivacyRingPreviewItem> PreviewItems { get; } = new();

	public Wallet Wallet { get; }

	private void RefreshCoinsList(IEnumerable<Pocket> pockets)
	{
		_itemsSourceList.Edit(list => CreateSegments(pockets, list));
	}

	private void CreateSegments(IEnumerable<Pocket> pockets, IExtendedList<PrivacyRingItemViewModel> list)
	{
		list.Clear();

		if (Width == 0d)
		{
			return;
		}

		var coinCount = pockets.SelectMany(x => x.Coins).Count();

		var result = Enumerable.Empty<PrivacyRingItemViewModel>();

		if (coinCount < UIConstants.PrivacyRingMaxItemCount)
		{
			result = CreateCoinSegments(pockets);
		}
		else
		{
			result = CreatePocketSegments(pockets);
		}

		foreach (var item in result)
		{
			list.Add(item);
		}

		PreviewItems.RemoveRange(1, PreviewItems.Count - 1);
		PreviewItems.AddRange(list);

		References.Clear();

		var references =
			list.GroupBy(x => (x.IsPrivate, x.IsSemiPrivate, x.IsNonPrivate, x.Unconfirmed))
				.Select(x => x.First())
				.OrderBy(list.IndexOf)
				.ToList();

		References.AddRange(references);
	}

	private IEnumerable<PrivacyRingItemViewModel> CreateCoinSegments(IEnumerable<Pocket> pockets)
	{
		var total = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));
		var start = 0.0m;

		var usablePockets =
				pockets.Where(x => x.Coins.Any())
					   .OrderByDescending(x => x.Coins.First().HdPubKey.AnonymitySet)
					   .ToList();

		foreach (var pocket in usablePockets)
		{
			var pocketCoins = pocket.Coins.OrderByDescending(x => x.Amount).ToList();

			foreach (var coin in pocketCoins)
			{
				var end = start + (Math.Abs(coin.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)) / total);

				var item = new PrivacyRingItemViewModel(this, coin, (double)start, (double)end);

				yield return item;

				start = end;
			}
		}
	}

	private IEnumerable<PrivacyRingItemViewModel> CreatePocketSegments(IEnumerable<Pocket> pockets)
	{
		var total = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));
		var start = 0.0m;

		var usablePockets =
				pockets.Where(x => x.Coins.Any())
					   .OrderByDescending(x => x.Coins.First().HdPubKey.AnonymitySet)
					   .ToList();

		foreach (var pocket in usablePockets)
		{
			var end = start + (Math.Abs(pocket.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)) / total);

			var item = new PrivacyRingItemViewModel(this, pocket, (double)start, (double)end);

			yield return item;

			_disposables.Add(item);

			start = end;
		}
	}

	public void Dispose()
	{
		_itemsSourceList?.Dispose();
		_disposables.Dispose();
	}
}
