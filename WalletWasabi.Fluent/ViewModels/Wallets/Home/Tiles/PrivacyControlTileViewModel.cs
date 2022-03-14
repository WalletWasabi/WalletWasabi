using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class PrivacyControlTileViewModel : TileViewModel
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly Wallet _wallet;

	[AutoNotify] private string _balancePrivateBtc = "";
	[AutoNotify] private string _balancePartiallyPrivateBtc = "";

	public PrivacyControlTileViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVm.Wallet;
		_balanceChanged = balanceChanged;
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_balanceChanged
			.Subscribe(_ => Update())
			.DisposeWith(disposables);
	}

	private void Update()
	{
		var privateThreshold = _wallet.KeyManager.MinAnonScoreTarget;
		var totalBalance = _wallet.Coins.TotalAmount().Satoshi;

		var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold);
		var privateBalance = privateCoins.Sum(x => x.Amount.Satoshi);
		var pcPrivate = totalBalance == 0M ? 100 : (privateBalance * 100 / totalBalance);
		BalancePrivateBtc = $"{privateCoins.TotalAmount().ToFormattedString()} BTC | {pcPrivate} %";

		var partiallyPrivateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet > 1 && x.HdPubKey.AnonymitySet < privateThreshold);
		var partiallyPrivateBalance = partiallyPrivateCoins.Sum(x => x.Amount.Satoshi);
		var pcPartiallyPrivate = totalBalance == 0M ? 100 : (partiallyPrivateBalance * 100 / totalBalance);
		BalancePartiallyPrivateBtc = $"{partiallyPrivateCoins.TotalAmount().ToFormattedString()} BTC | {pcPartiallyPrivate} %";
	}
}
