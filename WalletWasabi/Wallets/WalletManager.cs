using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets
{
	public class WalletManager
	{
		public WalletManager(string walletBackupsDir, string dataDir)
		{
			WalletBackupsDir = walletBackupsDir;
			DataDir = dataDir;
			Wallets = new Dictionary<WalletService, HashSet<uint256>>();
			Lock = new object();
			AddRemoveLock = new AsyncLock();
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		private Dictionary<WalletService, HashSet<uint256>> Wallets { get; }
		private object Lock { get; }
		private AsyncLock AddRemoveLock { get; }
		private string WalletBackupsDir { get; }
		private BitcoinStore BitcoinStore { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private NodesGroup Nodes { get; set; }
		private string DataDir { get; }
		private ServiceConfiguration ServiceConfiguration { get; set; }
		private IFeeProvider FeeProvider { get; set; }
		private CoreNode CoreNode { get; set; }
		private bool WalletCreationEnabled { get; set; }
		private CancellationTokenSource StoppingCts { get; set; } = new CancellationTokenSource();

		private IEnumerable<KeyValuePair<WalletService, HashSet<uint256>>> AliveWalletsNoLock => Wallets.Where(x => x.Key is { IsStoppingOrStopped: var isDisposed } && !isDisposed);

		public WalletService GetFirstOrDefaultWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.FirstOrDefault();
			}
		}

		public async Task<WalletService> AddWalletServiceAsync(KeyManager keyManager)
		{
			WalletService walletService;

			while (!WalletCreationEnabled)
			{
				await Task.Delay(100, StoppingCts.Token).ConfigureAwait(false);
			}

			walletService = new WalletService(BitcoinStore, keyManager, Synchronizer, Nodes, DataDir, ServiceConfiguration, FeeProvider, CoreNode);

			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				lock (Lock)
				{
					Wallets.Add(walletService, new HashSet<uint256>());
				}
			}
			return walletService;
		}

		public async Task StartWalletServiceAsync(WalletService walletService, CancellationToken cancel)
		{
			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				lock (Lock)
				{
					if (!Wallets.ContainsKey(walletService))
					{
						throw new InvalidOperationException("Add wallet first.");
					}
				}

				Logger.LogInfo($"Starting {nameof(WalletService)}...");
				await walletService.StartAsync(cancel).ConfigureAwait(false);
				Logger.LogInfo($"{nameof(WalletService)} started.");

				cancel.ThrowIfCancellationRequested();

				walletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
				walletService.ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;
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

		public void Init(BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, NodesGroup nodes, ServiceConfiguration serviceConfiguration, IFeeProvider feeProviders, CoreNode coreNode = null)
		{
			BitcoinStore = bitcoinStore;
			Synchronizer = synchronizer;
			Nodes = nodes;
			ServiceConfiguration = serviceConfiguration;
			FeeProvider = feeProviders;
			CoreNode = coreNode;

			WalletCreationEnabled = true;
		}

		public async Task RemoveAndStopAllAsync()
		{
			if (StoppingCts is { } && !StoppingCts.IsCancellationRequested)
			{
				StoppingCts.Cancel();
				StoppingCts.Dispose();
			}

			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				List<WalletService> walletsListClone;
				lock (Lock)
				{
					walletsListClone = Wallets.Keys.ToList();
				}
				foreach (var walletService in walletsListClone)
				{
					walletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
					walletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;

					lock (Lock)
					{
						if (!Wallets.Remove(walletService))
						{
							throw new InvalidOperationException("Wallet service doesn't exist.");
						}
					}

					var keyManager = walletService.KeyManager;
					if (keyManager is { } && !string.IsNullOrWhiteSpace(WalletBackupsDir))
					{
						string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(keyManager.FilePath));
						keyManager.ToFile(backupWalletFilePath);
						Logger.LogInfo($"{nameof(walletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
					}
					await walletService.StopAsync(CancellationToken.None).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(WalletService)} is stopped.");
				}
			}
		}

		public void ProcessCoinJoin(SmartTransaction tx)
		{
			lock (Lock)
			{
				foreach (var pair in AliveWalletsNoLock.Where(x => !x.Value.Contains(tx.GetHash())))
				{
					var walletService = pair.Key;
					pair.Value.Add(tx.GetHash());
					walletService.TransactionProcessor.Process(tx);
				}
			}
		}

		public void Process(SmartTransaction transaction)
		{
			lock (Lock)
			{
				foreach (var wallet in AliveWalletsNoLock)
				{
					wallet.Key.TransactionProcessor.Process(transaction);
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
					SmartCoin coin = walletService.Key.Coins.GetByOutPoint(input);
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
