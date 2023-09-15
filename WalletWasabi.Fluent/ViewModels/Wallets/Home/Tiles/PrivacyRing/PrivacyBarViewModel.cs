using DynamicData;
using DynamicData.Binding;
using System.Linq;
using NBitcoin;
using WalletWasabi.Wallets;
using System.Reactive.Disposables;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public partial class PrivacyBarViewModel : ActivatableViewModel
{
	private readonly WalletViewModel _walletViewModel;

	[AutoNotify] private bool _hasProgress;
	[AutoNotify] private decimal _totalAmount;

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
		Items.Clear();

		var itemsSourceList = new SourceList<PrivacyBarItemViewModel>();

		itemsSourceList
			.DisposeWith(disposables)
			.Connect()
			.Bind(Items)
			.Subscribe()
			.DisposeWith(disposables);

		_walletViewModel.UiTriggers.PrivacyProgressUpdateTrigger
			.Subscribe(_ => itemsSourceList.Edit(Update))
			.DisposeWith(disposables);
	}

	private void Update(IExtendedList<PrivacyBarItemViewModel> list)
	{
		TotalAmount = _walletViewModel.Wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC);

		list.Clear();

		var coinCount = _walletViewModel.Wallet.Coins.Count();

		if (coinCount == 0d)
		{
			return;
		}

		var segments =
			_walletViewModel.Wallet.Coins
								   .GroupBy(x => x.GetPrivacyLevel(_walletViewModel.Wallet))
								   .OrderBy(x => (int)x.Key)
								   .Select(x => new PrivacyBarItemViewModel(x.Key, x.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC))))
								   .ToList();

		HasProgress = segments.Any(x => x.PrivacyLevel != PrivacyLevel.NonPrivate);

		list.AddRange(segments);
	}
}
