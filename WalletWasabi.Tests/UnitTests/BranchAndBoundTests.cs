using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.BranchNBound;
using WalletWasabi.Tests.Helpers;
using System.Linq;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class BranchAndBoundTests
	{
		public BranchAndBoundTests()
		{
			KeyManager keyManager = ServiceFactory.CreateKeyManager();
			HdPubKey = BitcoinFactory.CreateHdPubKey(keyManager);
		}

		private Random Random { get; } = new();
		private HdPubKey HdPubKey { get; }

		[Fact]
		public void RandomizedTest()
		{
			List<SmartCoin> utxos = GenList();
			BranchAndBound selector = new(utxos);
			Money target = Money.Satoshis(100000000);

			bool successful = selector.TryGetExactMatch(target, out List<SmartCoin>? selectedCoins);

			Assert.True(successful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(target, selectedCoins!.Select(x => x.Amount).Sum());
		}

		[Fact]
		public void SimpleSelectionTest()
		{
			List<SmartCoin> utxos = new() { BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(120000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(50000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(40000)) };
			BranchAndBound selector = new(utxos);
			List<SmartCoin> expectedCoins = new() { BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(50000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(40000)) };
			Money target = Money.Satoshis(190000);

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<SmartCoin>? selectedCoins);

			Assert.True(wasSuccessful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void CanSelectEveryCoin()
		{
			List<SmartCoin> utxos = new() { BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(120000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)) };
			BranchAndBound selector = new(utxos);
			List<SmartCoin> expectedCoins = new() { BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(120000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)) };
			Money target = Money.Satoshis(320000);

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<SmartCoin>? selectedCoins);

			Assert.True(wasSuccessful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void TargetIsBiggerThanBalance()
		{
			List<SmartCoin> utxos = GenList();
			BranchAndBound selector = new(utxos);
			Money target = Money.Satoshis(11111111111111111);

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<SmartCoin>? selectedCoins);

			Assert.False(wasSuccessful);
			Assert.Null(selectedCoins);
		}

		[Fact]
		public void ReturnNullIfNoExactMatchFoundTest()
		{
			List<SmartCoin> utxos = new() { BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(120000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(100000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(50000)), BitcoinFactory.CreateSmartCoin(HdPubKey, Money.Satoshis(40000)) };
			BranchAndBound selector = new(utxos);
			Money target = Money.Satoshis(300000);

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<SmartCoin>? selectedCoins);

			Assert.False(wasSuccessful);
			Assert.Null(selectedCoins);
		}

		private List<SmartCoin> GenList()
		{
			KeyManager keyManager = ServiceFactory.CreateKeyManager();
			HdPubKey hdPubKey = BitcoinFactory.CreateHdPubKey(keyManager);
			List<SmartCoin> availableCoins = new();

			for (int i = 0; i < 1000; i++)
			{
				availableCoins.Add(BitcoinFactory.CreateSmartCoin(hdPubKey, amount: (ulong)Random.Next(1000, 99999999)));
			}
			return availableCoins;
		}
	}
}
