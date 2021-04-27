using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
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
		private readonly Wallet _wallet;
		private readonly IObservable<Unit> _balanceChanged;

		[AutoNotify] private IList<(string color, double percentShare)>? _testDataPoints;
		[AutoNotify] private IList<DataLegend>? _testDataPointsLegend;

		public WalletPieChartTileViewModel(Wallet wallet, IObservable<Unit> balanceChanged)
		{
			_balanceChanged = balanceChanged;
			_wallet = wallet;
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_balanceChanged
				.Subscribe(_ => Update())
				.DisposeWith(disposables);

			Update();
		}

		private void Update()
		{
			var privateThreshold = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();

			var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold);
			var normalCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold);
			var totalCount = (double) _wallet.Coins.Count();

			var pcPrivate = (privateCoins.Count() / totalCount);
			var pcNormal = (normalCoins.Count() / totalCount);

			TestDataPoints = new List<(string, double)>()
			{
				("#72BD81", pcPrivate),
				("#F9DE7D", pcNormal)
			};

			TestDataPointsLegend = new List<DataLegend>
			{
				new(privateCoins.TotalAmount(), "Private", "#72BD81", pcPrivate),
				new(normalCoins.TotalAmount(), "Not Private", "#F9DE7D", pcNormal)
			};
		}
	}
}
