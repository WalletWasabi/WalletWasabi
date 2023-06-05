using Avalonia;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

[NavigationMetaData(
	Title = "Privacy Progress",
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

	public PrivacyRingViewModel(UiContext uiContext, WalletViewModel walletViewModel)
	{
		UiContext = uiContext;
		_walletViewModel = walletViewModel;
		Wallet = walletViewModel.Wallet;

		NextCommand = CancelCommand;
		PrivacyTile = new PrivacyControlTileViewModel(UiContext, walletViewModel, false);
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
				.Do(_ => itemsSourceList.Clear())
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ToSignal();

		_walletViewModel.UiTriggers
						.PrivacyProgressUpdateTrigger
						.Merge(sizeTrigger)
						.ObserveOn(RxApp.MainThreadScheduler)
						.Subscribe(_ => RenderRing(itemsSourceList))
						.DisposeWith(disposables);
	}

	private void RenderRing(SourceList<PrivacyRingItemViewModel> list)
	{
		if (Width == 0d)
		{
			return;
		}

		SetMargins();

		list.Edit(list => CreateSegments(list));

		PreviewItems.RemoveMany(PreviewItems.OfType<PrivacyRingItemViewModel>());
		PreviewItems.AddRange(list.Items);

		SetReferences(list);
	}

	private void CreateSegments(IExtendedList<PrivacyRingItemViewModel> list)
	{
		list.Clear();

		var coinCount = _walletViewModel.Wallet.Coins.Count();

		var shouldCreateSegmentsByCoin = coinCount < UiConstants.PrivacyRingMaxItemCount;

		var result =
			shouldCreateSegmentsByCoin
			? CreateSegmentsByCoin()
			: CreateSegmentsByPrivacyLevel();

		list.AddRange(result);
	}

	private IEnumerable<PrivacyRingItemViewModel> CreateSegmentsByCoin()
	{
		var groupsByPrivacy = _walletViewModel.Wallet.Coins
			.GroupBy(x => x.GetPrivacyLevel(_walletViewModel.Wallet))
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

		var groupsByPrivacy = _walletViewModel.Wallet.Coins
			.GroupBy(x => x.GetPrivacyLevel(_walletViewModel.Wallet))
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

	private void SetMargins()
	{
		Margin = new Thickness(Width / 2, Height / 2, 0, 0);
		NegativeMargin = Margin * -1;
	}

	private void SetReferences(SourceList<PrivacyRingItemViewModel> list)
	{
		References.Clear();

		var references =
			list.Items.GroupBy(x => (x.IsPrivate, x.IsSemiPrivate, x.IsNonPrivate, x.Unconfirmed))
				.Select(x => x.First())
				.OrderBy(list.Items.IndexOf)
				.ToList();

		References.AddRange(references);
	}
}
