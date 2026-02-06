using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class PrivacyControlTileViewModel : ActivatableViewModel, IPrivacyRingPreviewItem
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private string _percentText = "";
	[AutoNotify] private Amount _balancePrivate;
	[AutoNotify] private bool _hasPrivateBalance;

	private PrivacyControlTileViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		_balancePrivate = _wallet.AmountProvider.Create(Money.Zero);

		var canShowDetails = _wallet.HasBalance;

		ShowDetailsCommand = ReactiveCommand.Create(ShowDetails, canShowDetails);

		PrivacyBar = new PrivacyBarViewModel(wallet);

		var coinList =
			_wallet.Coins.List
						 .Connect(suppressEmptyChangeSets: false); // coinList here is not subscribed to SmartCoin changes.
																   // Dynamic updates to SmartCoin properties won't be reflected in the UI.
																   // See CoinModel.SubscribeToCoinChanges().

		TotalAmount = coinList.Sum(set => set.Amount.ToDecimal(MoneyUnit.Satoshi));
		PrivateAmount = coinList.Filter(x => x.IsPrivate, suppressEmptyChangeSets: false).Sum(set => set.Amount.ToDecimal(MoneyUnit.Satoshi));
		SemiPrivateAndPrivateAmount = coinList.Filter(x => x.IsPrivate || x.IsSemiPrivate, suppressEmptyChangeSets: false).Sum(set => set.Amount.ToDecimal(MoneyUnit.Satoshi));
		IsPrivacyProgressDisplayed = SemiPrivateAndPrivateAmount.CombineLatest(TotalAmount, resultSelector: IsProgressVisible);
	}

	public IObservable<bool> IsPrivacyProgressDisplayed { get; }

	public ICommand ShowDetailsCommand { get; }

	public PrivacyBarViewModel? PrivacyBar { get; }

	public IObservable<decimal> SemiPrivateAndPrivateAmount { get; }

	public IObservable<decimal> PrivateAmount { get; }

	public IObservable<decimal> TotalAmount { get; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		var coinList =
			_wallet.Coins.List                                      // coinList here is not subscribed to SmartCoin changes.
						 .Connect(suppressEmptyChangeSets: false)   // Dynamic updates to SmartCoin properties won't be reflected in the UI.
						 .ToCollection();                           // See CoinModel.SubscribeToCoinChanges().

		_wallet.Privacy.Progress
					   .CombineLatest(_wallet.Privacy.IsWalletPrivate)
					   .CombineLatest(coinList)
					   .Flatten()
					   .Do(tuple =>
					   {
						   var (privacyProgress, isWalletPrivate, coins) = tuple;
						   Update(privacyProgress, isWalletPrivate, coins);
					   })
					   .Subscribe()
					   .DisposeWith(disposables);

		PrivacyBar?.Activate(disposables);
	}

	private static bool IsProgressVisible(decimal privateAndSemiPrivateBalance, decimal totalBalance)
	{
		var hasPrivacy = privateAndSemiPrivateBalance > 0;
		var hasBalance = totalBalance > 0;
		var isProgressVisible = hasPrivacy && hasBalance;
		return isProgressVisible;
	}

	private void ShowDetails()
	{
		UiContext.Navigate().To().PrivacyRing(_wallet);
	}

	private void Update(int privacyProgress, bool isWalletPrivate, IReadOnlyCollection<CoinModel> coins)
	{
		PercentText =
			coins.TotalAmount() > Money.Zero
			? $"{privacyProgress} %"
			: "N/A";

		FullyMixed = isWalletPrivate;

		BalancePrivate = _wallet.AmountProvider.Create(coins.Where(x => x.IsPrivate).TotalAmount());
		HasPrivateBalance = BalancePrivate.HasBalance;
	}
}
