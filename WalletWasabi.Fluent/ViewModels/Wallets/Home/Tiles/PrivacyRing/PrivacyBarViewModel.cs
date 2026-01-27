using DynamicData;
using DynamicData.Binding;
using System.Linq;
using NBitcoin;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public partial class PrivacyBarViewModel : ActivatableViewModel
{
	[AutoNotify] private bool _hasProgress;
	[AutoNotify] private decimal _totalAmount;

	public PrivacyBarViewModel(IWalletModel wallet)
	{
		Wallet = wallet;
	}

	public ObservableCollectionExtended<PrivacyBarItemViewModel> Items { get; } = new();

	public IWalletModel Wallet { get; }

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

		Wallet.Coins.List                                 // Wallet.Coins.List here is not subscribed to SmartCoin changes.
			.Connect(suppressEmptyChangeSets: false)      // Dynamic updates to SmartCoin properties won't be reflected in the UI.
			.ToCollection()                               // See CoinModel.SubscribeToCoinChanges().
			.Subscribe(x => itemsSourceList.Edit(l => Update(l, x)))
			.DisposeWith(disposables);
	}

	private void Update(IExtendedList<PrivacyBarItemViewModel> list, IReadOnlyCollection<ICoinModel> coins)
	{
		TotalAmount = coins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

		list.Clear();

		var coinCount = coins.Count;

		if (coinCount == 0d)
		{
			return;
		}

		var segments =
			coins
				.GroupBy(x => x.PrivacyLevel)
				.OrderBy(x => (int)x.Key)
				.Select(x => new PrivacyBarItemViewModel(x.Key, x.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC))))
				.ToList();

		HasProgress = segments.Any(x => x.PrivacyLevel != PrivacyLevel.NonPrivate);

		list.AddRange(segments);
	}
}
