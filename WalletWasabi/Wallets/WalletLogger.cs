using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;

namespace WalletWasabi.Wallets
{
	public class WalletLogger
	{
		public WalletLogger(string walletName, Network network, ICoinsView coins, string dataDir)
		{
			WalletName = walletName;
			Network = network;
			Coins = coins;
			if (network == Network.Main)
			{
				BlockExplorerPrefix = "https://mempool.space/tx/";
			}
			else if (network == Network.TestNet)
			{
				BlockExplorerPrefix = "https://mempool.space/testnet/tx/";
			}
			else
			{
				BlockExplorerPrefix = "";
			}

			FilePath = Path.Combine(dataDir, "WalletLogs", WalletName, $"WalletLog_{WalletName}_{DateTime.Now:yyyyMMdd}.txt");

			TryCreateFile(FilePath);

			LastBalance = Coins.TotalAmount();
		}

		private string WalletName { get; }
		private string BlockExplorerPrefix { get; }
		private string FilePath { get; }
		private Network Network { get; }
		public ICoinsView Coins { get; }
		private Money LastBalance { get; }

		public async Task LogAsync(ProcessedResult e)
		{
			var currentBalance = Coins.TotalAmount();

			StringBuilder sb = new();

			if (e.NewlySpentCoins.Any())
			{
				sb.AppendLine($"Spent");
				foreach (var coin in e.NewlySpentCoins.OrderByDescending(c => c.Amount))
				{
					sb.AppendLine($"{coin.Amount}({coin.HdPubKey.AnonymitySet})");
				}
			}

			if (e.NewlyReceivedCoins.Any())
			{
				sb.AppendLine($"Received");
				foreach (var coin in e.NewlyReceivedCoins.OrderByDescending(c => c.Amount))
				{
					sb.AppendLine($"{coin.Amount}({coin.HdPubKey.AnonymitySet})");
				}
			}

			if (e.ReceivedDusts.Any())
			{
				sb.AppendLine($"Received dust");
				foreach (var coin in e.ReceivedDusts.OrderByDescending(c => c.Value))
				{
					sb.AppendLine($"{coin.Value}");
				}
			}

			if (e.ReplacedCoins.Any() || e.RestoredCoins.Any() || e.SuccessfullyDoubleSpentCoins.Any())
			{
				sb.AppendLine($"ReplacedCoins ({e.ReplacedCoins.Count})");
				sb.AppendLine($"RestoredCoins ({e.RestoredCoins.Count})");
				sb.AppendLine($"SuccessfullyDoubleSpentCoins ({e.SuccessfullyDoubleSpentCoins.Count})");
			}

			if (sb.Length > 0)
			{
				StringBuilder header = new();
				header.AppendLine();
				header.AppendLine($"Balance change: { currentBalance - LastBalance } - Total balance: {Coins.TotalAmount()}");
				header.AppendLine($"TxId: {BlockExplorerPrefix}{e.Transaction.GetHash()} {DateTime.Now}");
				sb.Insert(0, header.ToString());
			}

			await File.AppendAllTextAsync(FilePath, sb.ToString()).ConfigureAwait(false);
		}

		private void TryCreateFile(string filePath)
		{
			if (File.Exists(filePath))
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(filePath);

			StringBuilder sb = new ();

			sb.AppendLine($"WARNING! This file created only for debugging purposes! In any other cases immediately disable WalletLogging feature!");
			sb.AppendLine($"WalletName: {WalletName} File created: {DateTime.Now}");

			sb.AppendLine($"Total balance: {Coins.TotalAmount()}");
			if (Coins.Any())
			{
				sb.AppendLine($"Coins ({Coins.Count()}):");
				foreach (var coin in Coins.OrderByDescending(c => c.Amount))
				{
					sb.AppendLine($"{coin.Amount}({coin.HdPubKey.AnonymitySet})");
				}
			}

			File.WriteAllText(filePath, sb.ToString());
		}
	}
}
