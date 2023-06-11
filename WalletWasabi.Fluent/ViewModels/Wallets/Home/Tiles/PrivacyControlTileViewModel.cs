using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;
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
		var privateThreshold = _wallet.AnonScoreTarget;

		var currentPrivacyScore = _wallet.Coins.Sum(x => x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		var maxPrivacyScore = _wallet.Coins.TotalAmount().Satoshi * (privateThreshold - 1);
		int pcPrivate = maxPrivacyScore == 0M ? 100 : (int)(currentPrivacyScore * 100 / maxPrivacyScore);

		PercentText = $"{pcPrivate} %";

		FullyMixed = pcPrivate >= 100;

		BalancePrivate = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		HasPrivateBalance = BalancePrivate > Money.Zero;
	}
}
