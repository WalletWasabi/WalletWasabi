using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Tests.Helpers
{
	public static class ServiceFactory
	{
		public static TransactionFactory TransactionFactory(
		   IEnumerable<(string Label, int KeyIndex, decimal Amount, bool Confirmed, int AnonymitySet)> coins,
		   bool allowUnconfirmed = true,
		   bool watchOnly = false)
		{
			var password = "foo";
			var keyManager = watchOnly ? WatchOnlyKeyManager() : KeyManager(password);

			keyManager.AssertCleanKeysIndexed();

			var coinArray = coins.ToArray();
			var keys = keyManager.GetKeys().Take(coinArray.Length).ToArray();
			for (int i = 0; i < coinArray.Length; i++)
			{
				var c = coinArray[i];
				var k = keys[c.KeyIndex];
				k.SetLabel(c.Label);
			}

			var scoins = coins.Select(x => BitcoinFactory.SmartCoin(keys[x.KeyIndex], x.Amount, 0, x.Confirmed, x.AnonymitySet)).ToArray();
			foreach (var coin in scoins)
			{
				foreach (var sameLabelCoin in scoins.Where(c => !c.HdPubKey.Label.IsEmpty && c.HdPubKey.Label == coin.HdPubKey.Label))
				{
					sameLabelCoin.HdPubKey.Cluster = coin.HdPubKey.Cluster;
				}
			}
			var coinsView = new CoinsView(scoins);
			var transactionStore = new AllTransactionStoreMock(workFolderPath: ".", Network.Main);
			return new TransactionFactory(Network.Main, keyManager, coinsView, transactionStore, password, allowUnconfirmed);
		}

		public static KeyManager KeyManager(string password = "blahblahblah")
			=> Blockchain.Keys.KeyManager.CreateNew(out var _, password);

		public static KeyManager WatchOnlyKeyManager()
			=> Blockchain.Keys.KeyManager.CreateNewWatchOnly(new Mnemonic(Wordlist.English, WordCount.Twelve).DeriveExtKey().Neuter());

		public static BlockchainAnalyzer BlockchainAnalyzer(int privacyLevelThreshold = 100)
			=> new BlockchainAnalyzer(privacyLevelThreshold);
	}
}
