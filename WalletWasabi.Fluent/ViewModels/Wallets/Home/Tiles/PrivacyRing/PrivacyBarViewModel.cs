using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public partial class PrivacyBarViewModel : ActivatableViewModel
{
	private const decimal GapBetweenSegments = 1.5m;
	private const decimal EnlargeThreshold = 2m;
	private const decimal EnlargeBy = 1m;
	private readonly WalletViewModel _walletViewModel;

	[ObservableProperty] private double _width;

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
			.Subscribe(_ => RenderBar(itemsSourceList))
			.DisposeWith(disposables);
	}

	private void RenderBar(SourceList<PrivacyBarItemViewModel> itemsSourceList)
	{
		Items.Clear();

		itemsSourceList.Edit(list => CreateSegments(list));
	}

	private void CreateSegments(IExtendedList<PrivacyBarItemViewModel> list)
	{
		list.Clear();

		var coinCount = _walletViewModel.Wallet.Coins.Count();

		if (Width == 0d || coinCount == 0d)
		{
			return;
		}

		var shouldCreateSegmentsByCoin = coinCount < UiConstants.PrivacyRingMaxItemCount;

		var result =
			shouldCreateSegmentsByCoin
			? CreateSegmentsByCoin()
			: CreateSegmentsByPrivacyLevel();

		list.AddRange(result);
	}

	private IEnumerable<PrivacyBarItemViewModel> CreateSegmentsByCoin()
	{
		var coinCount = _walletViewModel.Wallet.Coins.Count();
		var totalAmount = _walletViewModel.Wallet.Coins.Sum(x => Math.Abs(x.Amount.ToDecimal(MoneyUnit.BTC)));
		var usableWidth = (decimal)Width - (coinCount - 1) * GapBetweenSegments;

		// Calculate the width of the segments.
		var rawSegments =
			_walletViewModel.Wallet.Coins.Select(coin =>
			{
				var amount = coin.Amount.ToDecimal(MoneyUnit.BTC);
				var width = Math.Abs(usableWidth * amount / totalAmount);

				return (Coin: coin, Width: width);
			}).ToArray();

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
				return (Coin: x.Coin, Width: finalWidth);
			}).ToArray();
		}

		// Order the coins as they will be shown in the bar.
		rawSegments = rawSegments
			.OrderBy(x => x.Coin.GetPrivacyLevel(_walletViewModel.Wallet))
			.ThenByDescending(x => x.Width)
			.ToArray();

		var start = 0.0m;
		foreach (var (coin, width) in rawSegments)
		{
			yield return new PrivacyBarItemViewModel(this, coin, (double)start, (double)width);

			start += width + GapBetweenSegments;
		}
	}

	private IEnumerable<PrivacyBarItemViewModel> CreateSegmentsByPrivacyLevel()
	{
		var groupsByPrivacy =
			_walletViewModel.Wallet.Coins.GroupBy(x => x.GetPrivacyLevel(_walletViewModel.Wallet))
										 .OrderBy(x => (int)x.Key)
										 .ToList();

		var totalAmount = _walletViewModel.Wallet.Coins.Sum(x => Math.Abs(x.Amount.ToDecimal(MoneyUnit.BTC)));

		var usableWidth = (decimal)Width - (groupsByPrivacy.Count - 1) * GapBetweenSegments;

		// Calculate the width of the segments.
		var rawSegments = groupsByPrivacy.Select(group =>
		{
			var amount = group.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));
			var width = Math.Abs(usableWidth * amount / totalAmount);

			return (PrivacyLevel: group.Key, Width: width);
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
				return (PrivacyLevel: x.PrivacyLevel, Width: finalWidth);
			}).ToArray();
		}

		// Order the segments as they will be shown in the bar.
		rawSegments = rawSegments
			.OrderBy(x => x.PrivacyLevel)
			.ThenByDescending(x => x.Width)
			.ToArray();

		var start = 0.0m;
		foreach (var (privacyLevel, width) in rawSegments)
		{
			yield return new PrivacyBarItemViewModel(privacyLevel, (double)start, (double)width);

			start += width + GapBetweenSegments;
		}
	}
}
