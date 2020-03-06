using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Wallets
{
	public class WalletManager
	{
		public WalletManager(string walletBackupsDir)
		{
			WalletBackupsDir = walletBackupsDir;
			Wallets = new Dictionary<Wallet, HashSet<uint256>>();
			Lock = new object();
			AddRemoveLock = new AsyncLock();
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		private Dictionary<Wallet, HashSet<uint256>> Wallets { get; }
		private object Lock { get; }
		private AsyncLock AddRemoveLock { get; }
		private string WalletBackupsDir { get; }

		private IEnumerable<KeyValuePair<Wallet, HashSet<uint256>>> AliveWalletsNoLock => Wallets.Where(x => x.Key.WalletService is { IsStoppingOrStopped: var isDisposed } && !isDisposed);		

		public WalletService GetFirstOrDefaultWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.FirstOrDefault()?.WalletService;
			}
		}

		public async Task AddAndStartAsync(Wallet wallet, CancellationToken cancel)
		{
			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				lock (Lock)
				{
					Wallets.Add(wallet, new HashSet<uint256>());
				}

				Logger.LogInfo($"Starting {nameof(WalletService)}...");
				await wallet.WalletService.StartAsync(cancel).ConfigureAwait(false);
				Logger.LogInfo($"{nameof(WalletService)} started.");

				cancel.ThrowIfCancellationRequested();

				wallet.WalletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
				wallet.WalletService.ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;
			}
		}

		private void ChaumianClient_OnDequeue(object sender, DequeueResult e)
		{
			var handler = OnDequeue;
			handler?.Invoke(sender, e);
		}

		private void TransactionProcessor_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			var handler = WalletRelevantTransactionProcessed;
			handler?.Invoke(sender, e);
		}

		public async Task RemoveAndStopAsync(Wallet wallet)
		{
			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				wallet.WalletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				wallet.WalletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;

				lock (Lock)
				{
					if (!Wallets.Remove(wallet))
					{
						throw new InvalidOperationException("Wallet service doesn't exist.");
					}
				}

				var keyManager = wallet.WalletService.KeyManager;
				if (keyManager is { } && !string.IsNullOrWhiteSpace(WalletBackupsDir))
				{
					string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(keyManager.FilePath));
					keyManager.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(wallet.WalletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
				}
				await wallet.WalletService.StopAsync(CancellationToken.None).ConfigureAwait(false);
				Logger.LogInfo($"{nameof(WalletService)} is stopped.");
			}
		}

		public async Task DisposeAllInWalletDependentServicesAsync()
		{
			List<Wallet> walletsListClone;
			lock (Lock)
			{
				walletsListClone = Wallets.Keys.ToList();
			}
			foreach (var wallet in walletsListClone)
			{
				await wallet.DisposeInWalletDependentServicesAsync(this);
			}
		}

		public async Task RemoveAndStopAllAsync()
		{
			List<Wallet> walletsListClone;
			lock (Lock)
			{
				walletsListClone = Wallets.Keys.ToList();
			}
			foreach (var wallet in walletsListClone)
			{
				await RemoveAndStopAsync(wallet);
			}
		}

		public void ProcessCoinJoin(SmartTransaction tx)
		{
			lock (Lock)
			{
				foreach (var pair in AliveWalletsNoLock.Where(x => !x.Value.Contains(tx.GetHash())))
				{
					var wallet = pair.Key;
					pair.Value.Add(tx.GetHash());
					wallet.WalletService.TransactionProcessor.Process(tx);
				}
			}
		}

		public void Process(SmartTransaction transaction)
		{
			lock (Lock)
			{
				foreach (var wallet in AliveWalletsNoLock)
				{
					wallet.Key.WalletService.TransactionProcessor.Process(transaction);
				}
			}
		}

		public IEnumerable<SmartCoin> CoinsByOutPoint(OutPoint input)
		{
			lock (Lock)
			{
				var res = new List<SmartCoin>();
				foreach (var walletService in AliveWalletsNoLock)
				{
					SmartCoin coin = walletService.Key.WalletService.Coins.GetByOutPoint(input);
					res.Add(coin);
				}

				return res;
			}
		}

		public ISet<uint256> FilterUnknownCoinjoins(IEnumerable<uint256> cjs)
		{
			lock (Lock)
			{
				var unknowns = new HashSet<uint256>();
				foreach (var pair in AliveWalletsNoLock)
				{
					// If a wallet service doesn't know about the tx, then we add it for processing.
					foreach (var tx in cjs.Where(x => !pair.Value.Contains(x)))
					{
						unknowns.Add(tx);
					}
				}
				return unknowns;
			}
		}
	}
}
