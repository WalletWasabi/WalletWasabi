using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class PrivacyControlTileViewModel : ActivatableViewModel, IPrivacyRingPreviewItem
{
	private readonly WalletViewModel _walletVm;
	private readonly Wallet _wallet;
	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private string _percentText = "";
	[AutoNotify] private Money _balancePrivate = Money.Zero;
	[AutoNotify] private bool _hasPrivateBalance;
	[AutoNotify] private bool _showPrivacyBar;

	private PrivacyControlTileViewModel(WalletViewModel walletVm, bool showPrivacyBar = true)
	{
		_wallet = walletVm.Wallet;
		_walletVm = walletVm;
		_showPrivacyBar = showPrivacyBar;

		var showDetailsCanExecute =
			walletVm.WhenAnyValue(x => x.IsWalletBalanceZero)
					.Select(x => !x);

		ShowDetailsCommand = ReactiveCommand.Create(ShowDetails, showDetailsCanExecute);

		if (showPrivacyBar)
		{
			PrivacyBar = new PrivacyBarViewModel(_walletVm);
		}
	}

	public ICommand ShowDetailsCommand { get; }

	public PrivacyBarViewModel? PrivacyBar { get; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_walletVm.UiTriggers.PrivacyProgressUpdateTrigger
			.Subscribe(_ => Update())
			.DisposeWith(disposables);

		PrivacyBar?.Activate(disposables);
	}

	private void ShowDetails()
	{
		UiContext.Navigate().To().PrivacyRing(_walletVm);
	}

	private void Update()
	{
		var pcPrivate = (int)(_wallet.GetPrivacyPercentage() * 100);

		PercentText = $"{pcPrivate} %";

		FullyMixed = _wallet.IsWalletPrivate();

		BalancePrivate = _wallet.Coins.FilterBy(x => x.IsPrivate(_wallet.AnonScoreTarget)).TotalAmount();
		HasPrivateBalance = BalancePrivate > Money.Zero;
	}
}
