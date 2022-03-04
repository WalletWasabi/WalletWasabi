using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class PrivacyControlTileViewModel : TileViewModel
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly Wallet _wallet;
	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private IList<(string color, double percentShare)>? _testDataPoints;
	[AutoNotify] private IList<DataLegend>? _testDataPointsLegend;
	[AutoNotify] private string _percentText;
	[AutoNotify] private string _balancePrivateBtc = "";
	[AutoNotify] private bool _hasPrivateBalance;

	public PrivacyControlTileViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVm.Wallet;
		_balanceChanged = balanceChanged;
		_percentText = "";
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

		var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		var normalAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();

		var privateDecimalAmount = privateAmount.ToDecimal(MoneyUnit.BTC);
		var normalDecimalAmount = normalAmount.ToDecimal(MoneyUnit.BTC);
		var totalDecimalAmount = privateDecimalAmount + normalDecimalAmount;

		var pcPrivate = totalDecimalAmount == 0M ? 1d : (double)(privateDecimalAmount / totalDecimalAmount);
		var pcNormal = 1 - pcPrivate;

		PercentText = $"\u205F{(int)Math.Floor(pcPrivate * 100)}\u205F/\u205F{100}";

		FullyMixed = pcPrivate >= 1d;

		HasPrivateBalance = privateAmount > Money.Zero;

		BalancePrivateBtc = $"{privateAmount.ToFormattedString()} BTC";

		TestDataPoints = new List<(string, double)>
		{
			("#78A827", pcPrivate),
			("#D8DED7", pcNormal)
		};

		TestDataPointsLegend = new List<DataLegend>
		{
			new(privateAmount, "Private", "#78A827", pcPrivate),
			new(normalAmount, "Not Private", "#D8DED7", pcNormal)
		};
	}
}
