using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets
{
	public class WalletManager
	{
		private class WalletState
		{
			public WalletState(WalletService walletService)
			{
				WalletService = walletService;

				CancelWalletServiceInitialization = new CancellationTokenSource();
			}

			public WalletService WalletService { get; }

			public CancellationTokenSource CancelWalletServiceInitialization { get; set; }
		}

		public WalletManager(string walletBackupsDir)
		{
			WalletBackupsDir = walletBackupsDir;
			Wallets = new Dictionary<WalletState, HashSet<uint256>>();
			Lock = new object();
			AddRemoveLock = new AsyncLock();
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		private Dictionary<WalletState, HashSet<uint256>> Wallets { get; }
		private object Lock { get; }
		private AsyncLock AddRemoveLock { get; }
		private string WalletBackupsDir { get; }

		private IEnumerable<KeyValuePair<WalletState, HashSet<uint256>>> AliveWalletsNoLock => Wallets.Where(x => x.Key.WalletService is { IsStoppingOrStopped: var isDisposed } && !isDisposed);

		private BitcoinStore BitcoinStore { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private NodesGroup Nodes { get; set; }
		private string DataDir { get; set; }
		private ServiceConfiguration ServiceConfiguration { get; set; }
		private IFeeProvider FeeProvider { get; set; }
		private CoreNode BitcoinCoreNode { get; set; }

		public WalletService GetFirstOrDefaultWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.FirstOrDefault()?.WalletService;
			}
		}

		public async Task<WalletService> CreateAndStartWalletServiceAsync(KeyManager keyManager)
		{
			var walletState = new WalletState(new WalletService(BitcoinStore, keyManager, Synchronizer, Nodes, DataDir, ServiceConfiguration, FeeProvider, BitcoinCoreNode));
			var walletService = walletState.WalletService;
			var cancel = walletState.CancelWalletServiceInitialization.Token;

			using (walletState.CancelWalletServiceInitialization)
			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				lock (Lock)
				{
					Wallets.Add(walletState, new HashSet<uint256>());
				}
			}

			Logger.LogInfo($"Starting {nameof(WalletService)}...");
			await walletService.StartAsync(cancel).ConfigureAwait(false);
			Logger.LogInfo($"{nameof(WalletService)} started.");

			cancel.ThrowIfCancellationRequested();

			walletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
			walletService.ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;

			walletState.CancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.

			return walletService;
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

		public async Task RemoveAndStopAsync(WalletService service)
		{
			using (await AddRemoveLock.LockAsync().ConfigureAwait(false))
			{
				List<WalletState> walletsListClone;
				lock (Lock)
				{
					walletsListClone = Wallets.Keys.ToList();
				}

				// TODO make WalletService the key for the dictionary...
				var walletState = walletsListClone.FirstOrDefault(x => x.WalletService == service);

				try
				{
					walletState.CancelWalletServiceInitialization?.Cancel();
				}
				catch (ObjectDisposedException)
				{
					Logger.LogWarning($"{nameof(walletState.CancelWalletServiceInitialization)} is disposed. This can occur due to an error while processing the wallet.");
				}
				walletState.CancelWalletServiceInitialization = null;

				var walletService = walletState.WalletService;
				walletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				walletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;

				lock (Lock)
				{
					if (!Wallets.Remove(walletState))
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

		public async Task RemoveAndStopAllAsync()
		{
			List<WalletState> walletsListClone;
			lock (Lock)
			{
				walletsListClone = Wallets.Keys.ToList();
			}

			foreach (var walletState in walletsListClone)
			{
				await RemoveAndStopAsync(walletState.WalletService);
			}
		}

		public void ProcessCoinJoin(SmartTransaction tx)
		{
			lock (Lock)
			{
				foreach (var pair in AliveWalletsNoLock.Where(x => !x.Value.Contains(tx.GetHash())))
				{
					var walletService = pair.Key.WalletService;
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

		public void Initialize(BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, NodesGroup nodes, string dataDir, ServiceConfiguration serviceConfiguration, IFeeProvider feeProvider, CoreNode bitcoinCoreNode)
		{
			BitcoinStore = bitcoinStore;
			Synchronizer = synchronizer;
			Nodes = nodes;
			DataDir = dataDir;
			ServiceConfiguration = serviceConfiguration;
			FeeProvider = feeProvider;
			BitcoinCoreNode = bitcoinCoreNode;
		}
	}
}
