using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Wallet
{
	public class WalletManager
	{
		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> CoinsDequeued;

		public WalletManager(string walletBackupsDir, TransactionBroadcaster transactionBroadcaster, CoinJoinProcessor coinJoinProcessor)
		{
			WalletServices = new List<WalletService>();
			TransactionBroadcaster = transactionBroadcaster;
			CoinJoinProcessor = coinJoinProcessor;
			WalletBackupsDir = walletBackupsDir;
			WalletServicesLock = new object();
		}

		private object WalletServicesLock { get; }
		private List<WalletService> WalletServices { get; }
		public TransactionBroadcaster TransactionBroadcaster { get; private set; }
		public CoinJoinProcessor CoinJoinProcessor { get; private set; }
		public string WalletBackupsDir { get; }

		public IEnumerable<WalletService> GetWalletServices()
		{
			lock (WalletServicesLock)
			{
				return WalletServices.ToArray();
			}
		}

		public void Add(WalletService walletService)
		{
			lock (WalletServicesLock)
			{
				WalletServices.Add(walletService);
			}
		}

		public async Task StartAsync(WalletService walletService, CancellationToken token)
		{
			Logger.LogInfo($"Starting {nameof(WalletService)}...");
			await walletService.StartAsync(token);
			Logger.LogInfo($"{nameof(WalletService)} started.");

			token.ThrowIfCancellationRequested();
			TransactionBroadcaster.AddWalletService(walletService);
			CoinJoinProcessor.AddWalletService(walletService);

			walletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
			walletService.ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;
		}

		public async Task StopAsync(WalletService walletService, CancellationToken token)
		{
			walletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
			walletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;

			var keyManager = walletService.KeyManager;
			if (keyManager is { }) // This should never happen.
			{
				string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(keyManager.FilePath));
				keyManager.ToFile(backupWalletFilePath);
				Logger.LogInfo($"{nameof(walletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
			}
			await walletService.StopAsync(token).ConfigureAwait(false);
			Logger.LogInfo($"{nameof(WalletService)} is stopped.");
		}

		private void TransactionProcessor_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			var handler = WalletRelevantTransactionProcessed;
			handler?.Invoke(sender, e);
		}

		private void ChaumianClient_OnDequeue(object sender, DequeueResult e)
		{
			var handler = CoinsDequeued;
			handler?.Invoke(sender, e);
		}
	}
}
