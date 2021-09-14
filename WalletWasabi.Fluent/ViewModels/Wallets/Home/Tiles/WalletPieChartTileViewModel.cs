using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public readonly struct DataLegend
	{
		public DataLegend(Money amount, string label, string hexColor, double percentShare)
		{
			Amount = amount;
			Label = label;
			HexColor = hexColor;
			PercentShare = percentShare;
		}

		public Money Amount { get; }
		public string Label { get; }
		public string HexColor { get; }
		public double PercentShare { get; }
	}

	public partial class WalletPieChartTileViewModel : TileViewModel
	{
		private readonly IObservable<Unit> _balanceChanged;
		private readonly Wallet _wallet;

		[AutoNotify] private IList<(string color, double percentShare)>? _testDataPoints;
		[AutoNotify] private IList<DataLegend>? _testDataPointsLegend;
		[AutoNotify] private bool _isPrivacyProtected;
		[AutoNotify] private bool _isAutoCoinJoinEnabled;

		public WalletPieChartTileViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
		{
			_balanceChanged = balanceChanged;
			_wallet = walletVm.Wallet;

			walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin)
				.Subscribe(x => IsAutoCoinJoinEnabled = x);

			this.WhenAnyValue(x => x.IsAutoCoinJoinEnabled)
				.Subscribe(x => walletVm.Settings.AutoCoinJoin = x);
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
	}
}
