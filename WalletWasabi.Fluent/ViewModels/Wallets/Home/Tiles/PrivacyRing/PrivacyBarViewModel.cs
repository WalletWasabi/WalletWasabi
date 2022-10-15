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
	private readonly decimal _gapBetweenSegments = 1.5m;
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

		IsEmpty = _coinsUpdated
			.Select(_ => !Items.Any())
			.ReplayLastActive();
	}

	public IObservable<bool> IsEmpty { get; }

	public ObservableCollectionExtended<PrivacyBarItemViewModel> Items { get; } = new();

	public Wallet Wallet { get; }

	private void RefreshCoinsList(IEnumerable<Pocket> pockets)
	{
		_itemsSourceList.Edit(list => CreateSegments(list, pockets));
	}

	private void CreateSegments(IExtendedList<PrivacyBarItemViewModel> list, IEnumerable<Pocket> pockets)
	{
		list.Clear();

		var coinCount = pockets.SelectMany(x => x.Coins).Count();

		if (coinCount == 0d)
		{
			return;
		}

		var result = Enumerable.Empty<PrivacyBarItemViewModel>();

		if (coinCount < UIConstants.PrivacyRingMaxItemCount)
		{
			result = CreateCoinSegments(pockets, coinCount);
		}
		else
		{
			result = CreatePocketSegments(pockets);
		}

		foreach (var item in result)
		{
			list.Add(item);
		}
	}

	private IEnumerable<PrivacyBarItemViewModel> CreateCoinSegments(IEnumerable<Pocket> pockets, int coinCount)
	{
		var totalAmount = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));
		var usableWidth = (decimal)Width - (coinCount - 1) * _gapBetweenSegments;

		// Order the coins as they will be shown in the bar.
		var orderedCoins =
			pockets
				.Where(x => x.Coins.Any())
				.OrderByDescending(x => x.Coins.First().HdPubKey.AnonymitySet)
				.Select(x => x.Coins.OrderByDescending(x => x.Amount))
				.SelectMany(x => x)
				.ToArray();

		// Calculate the width of the segments.
		var rawSegments = orderedCoins.Select(coin =>
		{
			var amount = coin.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC);
			var width = Math.Abs(usableWidth * amount / totalAmount);

			return (Coin: coin, Width: width);
		}).ToArray();

		var segmentsToEnlarge = rawSegments.Where(x => x.Width < 2).ToArray();

		// Artificially enlarge segments smaller than 2 px in order to make them visible.
		// Meanwhile decrease those segments that are larger than 2 px on order the fit all in the bar.
		if (segmentsToEnlarge.Any())
		{
			var enlargeBy = 1m;
			var segmentsToReduce = rawSegments.Except(segmentsToEnlarge);
			var reduceBy = segmentsToEnlarge.Length * enlargeBy / segmentsToReduce.Count();

			rawSegments = rawSegments.Select(x =>
			{
				var finalWidth = x.Width < 2 ? x.Width + enlargeBy : x.Width - reduceBy;
				return (Coin: x.Coin, Width: finalWidth);
			}).ToArray();
		}

		var start = 0.0m;
		foreach (var tup in rawSegments)
		{
			var (coin, width) = tup;

			yield return new PrivacyBarItemViewModel(this, coin, (double)start, (double)width);

			start += width + _gapBetweenSegments;
		}
	}

	private IEnumerable<PrivacyBarItemViewModel> CreatePocketSegments(IEnumerable<Pocket> pockets)
	{
		var totalAmount = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));
		var usableWidth = (decimal)Width - (pockets.Count() - 1) * _gapBetweenSegments;

		// Order the pockets as they will be shown in the bar.
		var orderedPockets =
			pockets
				.Where(x => x.Coins.Any())
				.OrderByDescending(x => x.Coins.First().HdPubKey.AnonymitySet)
				.ToArray();

		// Calculate the width of the segments.
		var rawSegments = orderedPockets.Select(pocket =>
		{
			var amount = pocket.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC);
			var width = Math.Abs(usableWidth * amount / totalAmount);

			return (Pocket: pocket, Width: width);
		}).ToArray();

		var segmentsToEnlarge = rawSegments.Where(x => x.Width < 2).ToArray();

		// Artificially enlarge segments smaller than 2 px in order to make them visible.
		// Meanwhile decrease those segments that are larger than 2 px on order the fit all in the bar.
		if (segmentsToEnlarge.Any())
		{
			var enlargeBy = 1m;
			var segmentsToReduce = rawSegments.Except(segmentsToEnlarge);
			var reduceBy = segmentsToEnlarge.Length * enlargeBy / segmentsToReduce.Count();

			rawSegments = rawSegments.Select(x =>
			{
				var finalWidth = x.Width < 2 ? x.Width + enlargeBy : x.Width - reduceBy;
				return (Coin: x.Pocket, Width: finalWidth);
			}).ToArray();
		}

		var start = 0.0m;
		foreach (var tup in rawSegments)
		{
			var (pocket, width) = tup;

			yield return new PrivacyBarItemViewModel(pocket, Wallet, (double)start, (double)width);

			start += width + _gapBetweenSegments;
		}
	}

	public void Dispose()
	{
		_itemsSourceList.Dispose();
	}
}
