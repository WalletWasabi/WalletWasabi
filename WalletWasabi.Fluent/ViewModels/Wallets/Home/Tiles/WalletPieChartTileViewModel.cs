using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using NBitcoin;
using WalletWasabi.Gui;
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
		private readonly Config _config;
		
		[AutoNotify] private IList<(string color, double percentShare)>? _testDataPoints;
		[AutoNotify] private IList<DataLegend>? _testDataPointsLegend;

		public WalletPieChartTileViewModel(Wallet wallet, Config config, IObservable<Unit> balanceChanged)
		{
			_wallet = wallet;
			_config = config;

			balanceChanged.Subscribe(_ => Update());

			Update();
		}

		private void Update()
		{
			var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet > _config.PrivacyLevelStrong);
			var normalCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < _config.PrivacyLevelStrong);
			var totalCount = (double)_wallet.Coins.Count();

			var pcPrivate = (privateCoins.Count() / totalCount);
			var pcNormal = (normalCoins.Count() / totalCount);

			TestDataPoints = new List<(string, double)>()
			{
				("#72BD81", pcPrivate),
				("#F9DE7D", pcNormal)
			};

			TestDataPointsLegend = new List<DataLegend>
			{
				new (privateCoins.TotalAmount(), "Private",  "#F9DE7D",  pcPrivate),
				new (normalCoins.TotalAmount(), "Not Private",  "#72BD81",  pcNormal)
			};
		}
	}
}
