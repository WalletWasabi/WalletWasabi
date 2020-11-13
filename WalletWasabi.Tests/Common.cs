using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
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

		public static BlockchainAnalyzer RandomBlockchainAnalyzer(int privacyLevelThreshold = 100, Money? dustThreshold = null)
		{
			dustThreshold ??= Money.Satoshis(1);
			return new BlockchainAnalyzer(privacyLevelThreshold, dustThreshold);
		}

		public static SmartTransaction RandomSmartTransaction(int othersInputCount = 1, int othersOutputCount = 1, int ownInputCount = 0, int ownOutputCount = 0)
		{
			var km = RandomKeyManager().Item2;
			var tx = Transaction.Create(Network.Main);
			var walletInputs = new HashSet<SmartCoin>(ownInputCount);
			var walletOutputs = new HashSet<SmartCoin>(ownOutputCount);

			for (int i = 0; i < othersInputCount; i++)
			{
				var sc = RandomSmartCoin(RandomHdPubKey(km), 1m);
				tx.Inputs.Add(sc.OutPoint);
			}
			for (int i = 0; i < ownInputCount; i++)
			{
				var sc = RandomSmartCoin(RandomHdPubKey(km), 1m);
				tx.Inputs.Add(sc.OutPoint);
				walletInputs.Add(sc);
			}

			for (int i = 0; i < othersOutputCount; i++)
			{
				var sc = RandomSmartCoin(RandomHdPubKey(km), 1.00001m);
				tx.Outputs.Add(sc.TxOut);
			}
			for (int i = 0; i < ownOutputCount; i++)
			{
				var sc = RandomSmartCoin(RandomHdPubKey(km), 1.00001m);
				tx.Outputs.Add(sc.TxOut);
				walletOutputs.Add(sc);
			}

			var stx = new SmartTransaction(tx, Height.Mempool);

			foreach (var sc in walletInputs)
			{
				stx.WalletInputs.Add(sc);
			}

			foreach (var sc in walletOutputs)
			{
				stx.WalletOutputs.Add(sc);
			}

			return stx;
		}

		public static HdPubKey RandomHdPubKey(KeyManager km)
		{
			return km.GenerateNewKey(SmartLabel.Empty, KeyState.Clean, isInternal: false);
		}

		public static SmartCoin RandomSmartCoin(HdPubKey pubKey, decimal amountBtc, bool confirmed = true, int anonymitySet = 1)
			=> RandomSmartCoin(pubKey, Money.Coins(amountBtc), confirmed, anonymitySet);

		public static int RandomInt(int minInclusive, int maxInclusive)
			=> new Random().Next(minInclusive, maxInclusive + 1);

		public static SmartCoin RandomSmartCoin(HdPubKey pubKey, Money amount, bool confirmed = true, int anonymitySet = 1)
		{
			var height = confirmed ? new Height(RandomInt(0, 200)) : Height.Mempool;
			pubKey.SetKeyState(KeyState.Used);
			var tx = Transaction.Create(Network.Main);
			tx.Outputs.Add(new TxOut(amount, pubKey.P2wpkhScript));
			var stx = new SmartTransaction(tx, height);
			pubKey.AnonymitySet = anonymitySet;
			return new SmartCoin(stx, 0, pubKey);
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
				var c = coinArray[i];
				var k = keys[c.KeyIndex];
				k.SetLabel(c.Label);
			}

			var scoins = coins.Select(x => RandomSmartCoin(keys[x.KeyIndex], x.Amount, x.Confirmed, x.AnonymitySet)).ToArray();
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
	}
}
