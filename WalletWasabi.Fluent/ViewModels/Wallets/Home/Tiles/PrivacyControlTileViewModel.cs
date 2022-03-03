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
	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private string _percentText = "";
	[AutoNotify] private string _balancePrivateBtc = "";
	[AutoNotify] private bool _hasPrivateBalance;

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
		var privacyScore = _wallet.Coins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC) * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		var idealPrivacyScore = _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC) * (privateThreshold - 1);

		var pcPrivate = idealPrivacyScore == 0M ? 1d : (double)(privacyScore / idealPrivacyScore);

		PercentText = $"\u205F{(int)Math.Floor(pcPrivate * 100)}\u205F/\u205F{100}";

		FullyMixed = pcPrivate >= 1d;

		var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		HasPrivateBalance = privateAmount > Money.Zero;

		BalancePrivateBtc = $"{privateAmount.ToFormattedString()} BTC";
	}
}
