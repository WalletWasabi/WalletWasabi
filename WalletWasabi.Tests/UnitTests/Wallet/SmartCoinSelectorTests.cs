using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class SmartCoinSelectorTests
{
	private KeyManager KeyManager { get; }

	public SmartCoinSelectorTests()
	{
		KeyManager = KeyManager.Recover(new Mnemonic("all all all all all all all all all all all all"), "", Network.Main, KeyManager.GetAccountKeyPath(Network.Main));
	}

	[Fact]
	public void SelectsOnlyOneCoinWhenPossible()
	{
		var keys = Enumerable.Range(0, 9)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false));

		var cluster = new Cluster(keys);

		var smartCoins = keys
			.Select((key, i) => {
				var coin =BitcoinFactory.CreateSmartCoin(key, 0.1m * (i+1));
				coin.HdPubKey.Cluster = cluster;
				return coin; })
			.ToList();

		var selector = new SmartCoinSelector(smartCoins);
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.3m));

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.3m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void PreferLessCoinsOverExactAmount()
	{
		var keys = Enumerable.Range(0, 10)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false))
				.ToList();

		var cluster = new Cluster(keys);

		var smartCoins = keys
			.Select((key, i) => {
				var coin = BitcoinFactory.CreateSmartCoin(key, 0.1m * (i+1));
				coin.HdPubKey.Cluster = cluster;
				return coin; })
			.ToList();
		smartCoins.Add(BitcoinFactory.CreateSmartCoin(keys[0], 0.11m));

		var selector = new SmartCoinSelector(smartCoins);

		var someCoins = smartCoins.Select(x => x.Coin);
		var coinsToSpend = selector.Select(someCoins, Money.Coins(0.41m));

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.5m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void PreferSameScript()
	{
		var keys = Enumerable.Range(0, 12)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false))
				.ToList();

		var cluster = new Cluster(keys);

		var smartCoins = keys
			.ConvertAll(key => {
				var coin = BitcoinFactory.CreateSmartCoin(key, 0.2m);
				coin.HdPubKey.Cluster = cluster;
				return coin; });

		smartCoins.Add(BitcoinFactory.CreateSmartCoin(keys[0], 0.11m));

		var selector = new SmartCoinSelector(smartCoins);

		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.31m)).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(coinsToSpend[0].ScriptPubKey, coinsToSpend[1].ScriptPubKey);
		Assert.Equal(0.31m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferMorePrivateClusterScript()
	{
		var keys1 = Enumerable.Range(0, 5)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false))
				.ToList();

		var juanCluster = new Cluster(keys1);

		var coinsKnownByJuan = keys1
			.ConvertAll(key => {
				var coin = BitcoinFactory.CreateSmartCoin(key, 0.2m);
				coin.HdPubKey.Cluster = juanCluster;
				return coin; });

		var keys2 = Enumerable.Range(0, 2)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false))
				.ToList();

		var betoCluster = new Cluster(keys2);

		var coinsKnownByBeto = keys2
			.ConvertAll(key => {
				var coin = BitcoinFactory.CreateSmartCoin(key, 0.2m);
				coin.HdPubKey.Cluster = betoCluster;
				return coin; });

		var selector = new SmartCoinSelector(coinsKnownByJuan.Concat(coinsKnownByBeto).ToList());
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.3m)).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(0.4m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}
}