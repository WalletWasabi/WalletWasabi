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
		var maxPrivacyScore = _wallet.Coins.TotalAmount().Satoshi * (privateThreshold - 1);

		var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold);
		var privateScore = privateCoins.Sum(x => x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		int pcPrivate = maxPrivacyScore == 0M ? 100 : (int)(privateScore * 100 / maxPrivacyScore);
		BalancePrivateBtc = $"{privateCoins.TotalAmount().ToFormattedString()} BTC | {pcPrivate} %";

		var partiallyPrivateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet > 1 && x.HdPubKey.AnonymitySet < privateThreshold);
		var partiallyPrivateScore = partiallyPrivateCoins.Sum(x => x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		int pcPartiallyPrivate = maxPrivacyScore == 0M ? 100 : (int)(partiallyPrivateScore * 100 / maxPrivacyScore);
		BalancePartiallyPrivateBtc = $"{partiallyPrivateCoins.TotalAmount().ToFormattedString()} BTC | {pcPartiallyPrivate} %";
	}
}
