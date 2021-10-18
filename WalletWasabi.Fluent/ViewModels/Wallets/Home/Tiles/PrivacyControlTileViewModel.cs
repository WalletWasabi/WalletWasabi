using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class PrivacyControlTileViewModel : TileViewModel
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly Wallet _wallet;
	[AutoNotify] private bool _isAutoCoinJoinEnabled;
	[AutoNotify] private bool _isBoosting;
	[AutoNotify] private bool _boostButtonVisible;
	[AutoNotify] private IList<(string color, double percentShare)>? _testDataPoints;
	[AutoNotify] private IList<DataLegend>? _testDataPointsLegend;

	public PrivacyControlTileViewModel(Wallet wallet, IObservable<Unit> balanceChanged)
	{
		_wallet = wallet;
		_balanceChanged = balanceChanged;

		this.WhenAnyValue(x => x.IsAutoCoinJoinEnabled, x => x.IsBoosting)
			.Subscribe(x =>
			{
				var (autoCjEnabled, isBoosting) = x;

				BoostButtonVisible = !autoCjEnabled && !isBoosting;
			});

		BoostPrivacyCommand = ReactiveCommand.Create(() =>
		{
			wallet.AllowManualCoinJoin = true;
			IsBoosting = true;
		});

		CancelPrivacyBoostCommand = ReactiveCommand.Create(() =>
		{
			wallet.AllowManualCoinJoin = false;
			IsBoosting = false;
		});
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
		var privateThreshold = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();

		var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		var normalAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();

		var privateDecimalAmount = privateAmount.ToDecimal(MoneyUnit.BTC);
		var normalDecimalAmount = normalAmount.ToDecimal(MoneyUnit.BTC);
		var totalDecimalAmount = privateDecimalAmount + normalDecimalAmount;

		var pcPrivate = totalDecimalAmount == 0M ? 0d : (double)(privateDecimalAmount / totalDecimalAmount);
		var pcNormal = 1 - pcPrivate;

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

	public ICommand BoostPrivacyCommand { get; }

	public ICommand CancelPrivacyBoostCommand { get; }


}