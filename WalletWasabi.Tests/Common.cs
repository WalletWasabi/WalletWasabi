using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using WalletWasabi.Blockchain.Analysis.AnonymityEstimation;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor;

namespace WalletWasabi.Tests
{
	public static class Common
	{
		static Common()
		{
			Logger.SetFilePath(Path.Combine(DataDir, "Logs.txt"));
			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		public static EndPoint TorSocks5Endpoint => new IPEndPoint(IPAddress.Loopback, 9050);
		public static string TorLogsFile => Path.Combine(DataDir, "TorLogs.txt");
		public static string TorDistributionFolder => Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "TorDaemons");
		public static TorSettings TorSettings => new TorSettings(DataDir, TorLogsFile, TorDistributionFolder);

		public static string DataDir => EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));

		public static SmartTransaction RandomSmartTransaction()
		{
			var tx = Transaction.Create(Network.Main);
			tx.Outputs.Add(Money.Coins(1), new Key());
			var stx = new SmartTransaction(tx, Height.Mempool);
			return stx;
		}

		public static SmartCoin RandomSmartCoin(HdPubKey pubKey, decimal amount, bool confirmed = true, int anonymitySet = 1)
		{
			var height = confirmed ? new Height(new Random().Next(0, 200)) : Height.Mempool;
			pubKey.SetKeyState(KeyState.Used);
			var tx = Transaction.Create(Network.Main);
			tx.Outputs.Add(new TxOut(Money.Coins(amount), pubKey.P2wpkhScript));
			var stx = new SmartTransaction(tx, height);
			return new SmartCoin(stx, 0, pubKey, anonymitySet);
		}

		public static TransactionFactory CreateTransactionFactory(
		   IEnumerable<(string Label, int KeyIndex, decimal Amount, bool Confirmed, int AnonymitySet)> coins,
		   bool allowUnconfirmed = true,
		   bool watchOnly = false)
		{
			var (password, keyManager) = watchOnly ? RandomWatchOnlyKeyManager() : RandomKeyManager();

			keyManager.AssertCleanKeysIndexed();

			var coinArray = coins.ToArray();
			var keys = keyManager.GetKeys().Take(coinArray.Length).ToArray();
			for (int i = 0; i < coinArray.Length; i++)
			{
				var k = keys[i];
				var c = coinArray[i];
				k.SetLabel(c.Label);
			}

			var scoins = coins.Select(x => RandomSmartCoin(keys[x.KeyIndex], x.Amount, x.Confirmed, x.AnonymitySet)).ToArray();
			foreach (var coin in scoins)
			{
				foreach (var sameLabelCoin in scoins.Where(c => !c.HdPubKey.Label.IsEmpty && c.HdPubKey.Label == coin.HdPubKey.Label))
				{
					sameLabelCoin.Cluster = coin.Cluster;
				}
			}
			var coinsRegistry = new CoinsRegistry(100);
			foreach (var c in scoins)
			{
				coinsRegistry.TryAdd(c);
			}
			var anonimityEstimator = new AnonymityEstimator(coinsRegistry, keyManager, Money.Satoshis(1));

			var transactionStore = new AllTransactionStoreMock(workFolderPath: ".", Network.Main);
			return new TransactionFactory(Network.Main, keyManager, coinsRegistry, anonimityEstimator, transactionStore, password, allowUnconfirmed);
		}

		public static (string, KeyManager) RandomKeyManager()
		{
			var password = "blahblahblah";
			return (password, KeyManager.CreateNew(out var _, password));
		}

		public static (string, KeyManager) RandomWatchOnlyKeyManager()
		{
			var (password, keyManager) = RandomKeyManager();
			return (password, KeyManager.CreateNewWatchOnly(keyManager.ExtPubKey));
		}

		public static string GetWorkDir([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			return Path.Combine(DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
		}

		internal static uint256 RandomUint256()
		{
			var random = new InsecureRandom();
			var randomBytes = random.GetBytes(32);

			return new uint256(randomBytes);
		}
	}
}
