using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public partial class PrivacyBarViewModel : ActivatableViewModel
{
	private const decimal GapBetweenSegments = 1.5m;
	private const decimal EnlargeThreshold = 2m;
	private const decimal EnlargeBy = 1m;
	private readonly WalletViewModel _walletViewModel;

	[AutoNotify] private double _width;

	public PrivacyBarViewModel(WalletViewModel walletViewModel)
	{
		_walletViewModel = walletViewModel;
		Wallet = walletViewModel.Wallet;
	}

	public ObservableCollectionExtended<PrivacyBarItemViewModel> Items { get; } = new();

	public Wallet Wallet { get; }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnActivated(CompositeDisposable disposables)
	{
		var itemsSourceList = new SourceList<PrivacyBarItemViewModel>();

		itemsSourceList
			.DisposeWith(disposables)
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(Items)
			.DisposeMany()
			.Subscribe()
			.DisposeWith(disposables);

		_walletViewModel.UiTriggers.PrivacyProgressUpdateTrigger
			.CombineLatest(this.WhenAnyValue(x => x.Width))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => RefreshCoinsList(itemsSourceList))
			.DisposeWith(disposables);
	}

	private void RefreshCoinsList(SourceList<PrivacyBarItemViewModel> itemsSourceList)
	{
		itemsSourceList.Edit(list => CreateSegments(list));
	}

	private void CreateSegments(IExtendedList<PrivacyBarItemViewModel> list)
	{
		Items.Clear();
		list.Clear();

		var pockets = _walletViewModel.Wallet.GetPockets();

		var coinCount = pockets.SelectMany(x => x.Coins).Count();

		if (Width == 0d || coinCount == 0d)
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
		var totalAmount = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(MoneyUnit.BTC)));
		var usableWidth = (decimal)Width - (coinCount - 1) * GapBetweenSegments;

		// Calculate the width of the segments.
		var rawSegments = pockets
			.Where(x => x.Coins.Any())
			.SelectMany(pocket => pocket.Coins.Select(coin =>
			{
				var amount = coin.Amount.ToDecimal(MoneyUnit.BTC);
				var width = Math.Abs(usableWidth * amount / totalAmount);

				return (OwnerPocket: pocket, Coin: coin, Width: width);
			})).ToArray();

		// Artificially enlarge segments smaller than the threshold px in order to make them visible.
		// Meanwhile decrease those segments that are larger than threshold px in order to fit all in the bar.
		var segmentsToEnlarge = rawSegments.Where(x => x.Width < EnlargeThreshold).ToArray();
		var segmentsToReduce = rawSegments.Except(segmentsToEnlarge).ToArray();
		var reduceBy = segmentsToEnlarge.Length * EnlargeBy / segmentsToReduce.Length;
		if (segmentsToEnlarge.Any() && segmentsToReduce.Any() && segmentsToReduce.All(x => x.Width - reduceBy > 0))
		{
			rawSegments = rawSegments.Select(x =>
			{
				var finalWidth = x.Width < EnlargeThreshold ? x.Width + EnlargeBy : x.Width - reduceBy;
				return (OwnerPocket: x.OwnerPocket, Coin: x.Coin, Width: finalWidth);
			}).ToArray();
		}

		// Order the coins as they will be shown in the bar.
		rawSegments = rawSegments
			.OrderByDescending(x => x.OwnerPocket.Coins.First().HdPubKey.AnonymitySet)
			.ThenByDescending(x => x.Width)
			.ToArray();

		var start = 0.0m;
		foreach (var (_, coin, width) in rawSegments)
		{
			yield return new PrivacyBarItemViewModel(this, coin, (double)start, (double)width);

			start += width + GapBetweenSegments;
		}
	}

	private IEnumerable<PrivacyBarItemViewModel> CreatePocketSegments(IEnumerable<Pocket> pockets)
	{
		var totalAmount = pockets.Sum(x => Math.Abs(x.Amount.ToDecimal(MoneyUnit.BTC)));
		var usableWidth = (decimal)Width - (pockets.Count() - 1) * GapBetweenSegments;

		// Calculate the width of the segments.
		var rawSegments = pockets.Select(pocket =>
		{
			var amount = pocket.Amount.ToDecimal(MoneyUnit.BTC);
			var width = Math.Abs(usableWidth * amount / totalAmount);

			return (Pocket: pocket, Width: width);
		}).ToArray();

		// Artificially enlarge segments smaller than threshold px in order to make them visible.
		// Meanwhile decrease those segments that are larger than threshold px in order to fit all in the bar.
		var segmentsToEnlarge = rawSegments.Where(x => x.Width < EnlargeThreshold).ToArray();
		var segmentsToReduce = rawSegments.Except(segmentsToEnlarge).ToArray();
		var reduceBy = segmentsToEnlarge.Length * EnlargeBy / segmentsToReduce.Length;
		if (segmentsToEnlarge.Any() && segmentsToReduce.Any() && segmentsToReduce.All(x => x.Width - reduceBy > 0))
		{
			rawSegments = rawSegments.Select(x =>
			{
				var finalWidth = x.Width < EnlargeThreshold ? x.Width + EnlargeBy : x.Width - reduceBy;
				return (Coin: x.Pocket, Width: finalWidth);
			}).ToArray();
		}

		// Order the pockets as they will be shown in the bar.
		rawSegments = rawSegments
			.OrderByDescending(x => x.Pocket.Coins.First().HdPubKey.AnonymitySet)
			.ThenByDescending(x => x.Width)
			.ToArray();

		var start = 0.0m;
		foreach (var (pocket, width) in rawSegments)
		{
			yield return new PrivacyBarItemViewModel(pocket, Wallet, (double)start, (double)width);

			start += width + GapBetweenSegments;
		}
	}
}
