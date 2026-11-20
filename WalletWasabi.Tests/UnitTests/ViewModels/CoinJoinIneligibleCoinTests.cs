using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

/// <summary>
/// The coin list tells which coins the last coinjoin round did not accept, and why (issue #11416).
/// </summary>
public class CoinJoinIneligibleCoinTests
{
	private const string Prefix = "Can't participate in coinjoin: ";

	private static readonly IServices Services = DispatchProxy.Create<IServices, ServicesProxy>();

	private static readonly KeyManager WalletKeys = ServiceFactory.CreateKeyManager("");

	[Fact]
	public void CoinTellsWhyItCannotCoinjoin()
	{
		string? GetToolTip(CoinJoinIneligibility? ineligibility)
		{
			var coin = CreateCoin(Money.Satoshis(4_000));
			coin.CoinJoinIneligibility = ineligibility;
			return CreateCoinModel(coin).CoinJoinIneligibilityToolTip;
		}

		Assert.Null(GetToolTip(null));

		Assert.Equal(
			Prefix + "the amount is below the coordinator's minimum of 0.00 005 000 BTC",
			GetToolTip(new CoinJoinIneligibility.AmountTooLow(Money.Satoshis(5_000))));

		Assert.Equal(
			Prefix + "the amount is above the coordinator's maximum of 43 000.00 000 000 BTC",
			GetToolTip(new CoinJoinIneligibility.AmountTooHigh(Money.Coins(43_000m))));

		Assert.Equal(
			Prefix + "at the last round's mining fee rate of 85 sat/vB it costs more to spend than it is worth",
			GetToolTip(new CoinJoinIneligibility.Uneconomical(new FeeRate(85m))));

		Assert.Equal(
			Prefix + "the coordinator doesn't accept SegWit coins",
			GetToolTip(new CoinJoinIneligibility.ScriptTypeNotAllowed()));
	}

	/// <summary>
	/// The rows of an open coin list follow what the coinjoin rounds leave on the coins.
	/// </summary>
	[Fact]
	public void CoinListFollowsTheCoins()
	{
		var tooSmallCoin = CreateCoin(Money.Satoshis(4_000));
		var otherCoin = CreateCoin(Money.Coins(1m));
		var loneCoin = CreateCoin(Money.Satoshis(3_000));

		using var coinListModel = new InMemoryCoinListModel(
			("Pocket of two", [tooSmallCoin, otherCoin]),
			("Pocket of one", [loneCoin]));

		// The rows do not navigate, copy to the clipboard or get rendered here.
		using var coinList = new CoinListViewModel(
			null!,
			coinListModel,
			new List<CoinModel>(),
			allowCoinjoiningCoinSelection: true,
			ignorePrivacyMode: false,
			allowSelection: false);

		var pocketRow = Assert.Single(coinList.TreeDataGridSource.Items.OfType<PocketViewModel>());
		var tooSmallCoinRow = pocketRow.Children.Single(x => x.Coin.GetSmartCoin() == tooSmallCoin);
		var otherCoinRow = pocketRow.Children.Single(x => x.Coin.GetSmartCoin() == otherCoin);
		var loneCoinRow = Assert.Single(coinList.TreeDataGridSource.Items.OfType<CoinViewModel>());

		Assert.All<CoinListItem>([pocketRow, tooSmallCoinRow, otherCoinRow, loneCoinRow], row => Assert.Null(row.CoinJoinIneligibilityToolTip));

		// A coinjoin round turns two of the coins down.
		tooSmallCoin.CoinJoinIneligibility = new CoinJoinIneligibility.AmountTooLow(Money.Satoshis(5_000));
		loneCoin.CoinJoinIneligibility = new CoinJoinIneligibility.AmountTooLow(Money.Satoshis(5_000));

		Assert.Equal(Prefix + "the amount is below the coordinator's minimum of 0.00 005 000 BTC", tooSmallCoinRow.CoinJoinIneligibilityToolTip);
		Assert.Equal(Prefix + "the amount is below the coordinator's minimum of 0.00 005 000 BTC", loneCoinRow.CoinJoinIneligibilityToolTip);
		Assert.Null(otherCoinRow.CoinJoinIneligibilityToolTip);
		Assert.Equal("Some coins can't participate in coinjoin", pocketRow.CoinJoinIneligibilityToolTip);

		// A later round accepts them.
		tooSmallCoin.CoinJoinIneligibility = null;
		loneCoin.CoinJoinIneligibility = null;

		Assert.All<CoinListItem>([pocketRow, tooSmallCoinRow, otherCoinRow, loneCoinRow], row => Assert.Null(row.CoinJoinIneligibilityToolTip));
	}

	private static SmartCoin CreateCoin(Money amount) =>
		BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(WalletKeys), amount);

	private static CoinModel CreateCoinModel(SmartCoin coin) =>
		new(coin, Network.Main, anonScoreTarget: 10, Services);

	/// <summary>
	/// The coins of a wallet, without the wallet.
	/// </summary>
	private sealed class InMemoryCoinListModel : ICoinListModel, IDisposable
	{
		private readonly SourceCache<CoinModel, int> _coins = new(x => x.Key);
		private readonly SourceCache<Pocket, LabelsArray> _pockets = new(x => x.Labels);

		public InMemoryCoinListModel(params (string Label, SmartCoin[] Coins)[] pockets)
		{
			_coins.AddOrUpdate(pockets.SelectMany(x => x.Coins).Select(CreateCoinModel));
			_pockets.AddOrUpdate(pockets.Select(x => new Pocket((new LabelsArray(x.Label), new CoinsView(x.Coins)))));
		}

		public IObservableCache<CoinModel, int> List => _coins;

		public IObservableCache<Pocket, LabelsArray> Pockets => _pockets;

		public CoinModel GetCoinModel(SmartCoin smartCoin) =>
			_coins.Items.Single(x => x.GetSmartCoin() == smartCoin);

		public void Dispose()
		{
			_pockets.Dispose();
			_coins.Dispose();
		}
	}

	/// <summary>
	/// Coins only ask for the height of the chain tip.
	/// </summary>
	public class ServicesProxy : DispatchProxy
	{
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
			targetMethod?.Name == nameof(IServices.GetServerTipHeight)
				? 1_000u
				: throw new NotSupportedException(targetMethod?.Name);
	}
}
