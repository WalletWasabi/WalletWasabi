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
		var keys = Enumerable.Range(0, 10)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false));

		var cluster = new Cluster(keys);

		var smartCoins = keys
			.Select((key, i) => {
				var coin =BitcoinFactory.CreateSmartCoin(key, 0.1m * (i+1));
				coin.HdPubKey.Cluster = cluster;
				return coin; });

		var selector = new SmartCoinSelector(smartCoins);

		var someCoins = smartCoins.Take(5).Select(x => x.Coin);
		var coinsToSpend = selector.Select(someCoins, Money.Coins(0.3m));

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.3m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void PreferLessCoinsOverExactAmount()
	{
		var keys = Enumerable.Range(0, 5)
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
		var keys = Enumerable.Range(0, 5)
				.Select(_ => KeyManager.GenerateNewKey(new SmartLabel("Juan"), KeyState.Clean, false))
				.ToList();

		var cluster = new Cluster(keys);

		var smartCoins = keys
			.Select((key, i) => {
				var coin = BitcoinFactory.CreateSmartCoin(key, 0.2m);
				coin.HdPubKey.Cluster = cluster;
				return coin; })
			.ToList();
		smartCoins.Add(BitcoinFactory.CreateSmartCoin(keys[0], 0.11m));

		var selector = new SmartCoinSelector(smartCoins);

		var someCoins = smartCoins.Select(x => x.Coin);
		var coinsToSpend = selector.Select(someCoins, Money.Coins(0.31m)).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(coinsToSpend[0].ScriptPubKey, coinsToSpend[1].ScriptPubKey);
		Assert.Equal(0.31m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}
}