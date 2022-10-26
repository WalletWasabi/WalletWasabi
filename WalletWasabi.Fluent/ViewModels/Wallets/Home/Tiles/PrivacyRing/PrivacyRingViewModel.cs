using Avalonia;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

[NavigationMetaData(
	Title = "Wallet Coins",
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class PrivacyRingViewModel : RoutableViewModel
{
	private readonly CompositeDisposable _disposables = new();
	private readonly WalletViewModel _walletViewModel;

	[AutoNotify] private PrivacyRingItemViewModel? _selectedItem;
	[AutoNotify] private double _height;
	[AutoNotify] private double _width;
	[AutoNotify] private Thickness _margin;
	[AutoNotify] private Thickness _negativeMargin;

	public PrivacyRingViewModel(WalletViewModel walletViewModel)
	{
		_walletViewModel = walletViewModel;
		Wallet = walletViewModel.Wallet;

		NextCommand = CancelCommand;
		PrivacyTile = new PrivacyControlTileViewModel(walletViewModel, false);
		PrivacyTile.Activate(_disposables);

		PreviewItems.Add(PrivacyTile);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public PrivacyControlTileViewModel PrivacyTile { get; }

	public ObservableCollectionExtended<PrivacyRingItemViewModel> Items { get; } = new();
	public ObservableCollectionExtended<PrivacyRingItemViewModel> References { get; } = new();
	public ObservableCollectionExtended<IPrivacyRingPreviewItem> PreviewItems { get; } = new();

	public Wallet Wallet { get; }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var itemsSourceList = new SourceList<PrivacyRingItemViewModel>();

		itemsSourceList
			.DisposeWith(disposables)
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(Items)
			.DisposeMany()
			.Subscribe()
			.DisposeWith(disposables);

		var sizeTrigger =
			this.WhenAnyValue(x => x.Width, x => x.Height)
				.Where(tuple => tuple.Item1 != 0 && tuple.Item2 != 0)
				.Throttle(TimeSpan.FromMilliseconds(100));

		_walletViewModel.UiTriggers
						.PrivacyProgressUpdateTrigger
						.CombineLatest(sizeTrigger)
						.ObserveOn(RxApp.MainThreadScheduler)
						.Subscribe(x =>
						{
							SetMargins(x.Second.Item1, x.Second.Item2);
							itemsSourceList.Edit(list => CreateSegments(list));
						})
						.DisposeWith(disposables);
	}

	private void CreateSegments(IExtendedList<PrivacyRingItemViewModel> list)
	{
		Items.Clear();
		list.Clear();

		if (Width == 0d)
		{
			return;
		}

		var coinCount = _walletViewModel.Wallet.Coins.Count();

		var result = Enumerable.Empty<PrivacyRingItemViewModel>();

		if (coinCount < UIConstants.PrivacyRingMaxItemCount)
		{
			result = CreateCoinSegments();
		}
		else
		{
			result = CreateSegmentsByPrivacyLevel();
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

	private IEnumerable<PrivacyRingItemViewModel> CreateCoinSegments()
	{
		var groupsByPrivacy =
			_walletViewModel.Wallet.Coins.GroupBy(x => x.GetPrivacyLevel(_walletViewModel.Wallet))
										 .OrderBy(x => (int)x.Key)
										 .ToList();

		var total = _walletViewModel.Wallet.Coins.Sum(x => Math.Abs(x.Amount.ToDecimal(MoneyUnit.BTC)));
		var start = 0.0m;

		foreach (var group in groupsByPrivacy)
		{
			var coins = group.OrderByDescending(x => x.Amount).ToList();

			foreach (var coin in coins)
			{
				var end = start + (Math.Abs(coin.Amount.ToDecimal(MoneyUnit.BTC)) / total);

				var item = new PrivacyRingItemViewModel(this, coin, (double)start, (double)end);

				yield return item;

				start = end;
			}
		}
	}

	private IEnumerable<PrivacyRingItemViewModel> CreateSegmentsByPrivacyLevel()
	{
		var total = _walletViewModel.Wallet.Coins.Sum(x => Math.Abs(x.Amount.ToDecimal(MoneyUnit.BTC)));
		var start = 0.0m;

		var groupsByPrivacy =
			_walletViewModel.Wallet.Coins.GroupBy(x => x.GetPrivacyLevel(_walletViewModel.Wallet))
										 .OrderBy(x => (int)x.Key)
										 .ToList();

		foreach (var group in groupsByPrivacy)
		{
			var groupAmount =
				group.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

			var end = start + (Math.Abs(groupAmount) / total);

			var item = new PrivacyRingItemViewModel(this, group.Key, new Money(groupAmount, MoneyUnit.BTC), (double)start, (double)end);

			yield return item;

			_disposables.Add(item);

			start = end;
		}
	}

	private void SetMargins(double width, double height)
	{
		Margin = new Thickness(width / 2, height / 2, 0, 0);
		NegativeMargin = new Thickness(Margin.Left * -1, Margin.Top * -1, 0, 0);
	}
}
