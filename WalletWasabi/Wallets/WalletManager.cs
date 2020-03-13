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
		public WalletManager(WalletDirectories walletDirectories)
		{
			Wallets = new Dictionary<WalletService, HashSet<uint256>>();
			Lock = new object();
			AddRemoveLock = new AsyncLock();
			CancelAllInitialization = new CancellationTokenSource();
			WalletDirectories = walletDirectories;
		}

		private CancellationTokenSource CancelAllInitialization { get; }

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		private Dictionary<WalletService, HashSet<uint256>> Wallets { get; }
		private object Lock { get; }
		private AsyncLock AddRemoveLock { get; }

		private IEnumerable<KeyValuePair<WalletService, HashSet<uint256>>> AliveWalletsNoLock => Wallets.Where(x => x.Key is { IsStoppingOrStopped: var isDisposed } && !isDisposed);

		private BitcoinStore BitcoinStore { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private NodesGroup Nodes { get; set; }
		private string DataDir { get; set; }
		private ServiceConfiguration ServiceConfiguration { get; set; }

		public void SignalQuitPending(bool isQuitPending)
		{
			lock (Lock)
			{
				foreach (var client in Wallets.Keys.Select(x => x.ChaumianClient))
				{
					client.IsQuitPending = isQuitPending;
				}
			}
		}

		private IFeeProvider FeeProvider { get; set; }
		private CoreNode BitcoinCoreNode { get; set; }
		public WalletDirectories WalletDirectories { get; }

		public WalletService GetFirstOrDefaultWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.FirstOrDefault();
			}
		}

		public bool AnyWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.Any();
			}
		}

		public async Task<WalletService> CreateAndStartWalletServiceAsync(KeyManager keyManager)
		{
			using (await AddRemoveLock.LockAsync(CancelAllInitialization.Token).ConfigureAwait(false))
			{
				WalletService walletService = null;
				try
				{
					walletService = new WalletService(BitcoinStore, keyManager, Synchronizer, Nodes, DataDir, ServiceConfiguration, FeeProvider, BitcoinCoreNode);

					var cancel = CancelAllInitialization.Token;
					lock (Lock)
					{
						Wallets.Add(walletService, new HashSet<uint256>());
					}

					Logger.LogInfo($"Starting {nameof(WalletService)}...");
					await walletService.StartAsync(cancel).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(WalletService)} started.");

					cancel.ThrowIfCancellationRequested();

					walletService.TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
					walletService.ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;

					return walletService;
				}
				catch (Exception)
				{
					if (walletService is { })
					{
						walletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
						walletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;
						lock (Lock)
						{
							Wallets.Remove(walletService);
						}
						await walletService.StopAsync(CancellationToken.None).ConfigureAwait(false);
					}
					throw;
				}
			}
		}

		public async Task DequeueAllCoinsGracefullyAsync(DequeueReason reason, CancellationToken token)
		{
			IEnumerable<Task> tasks = null;
			lock (Lock)
			{
				tasks = Wallets.Keys.Select(x => x.ChaumianClient.DequeueAllCoinsFromMixGracefullyAsync(reason, token)).ToArray();
			}
			await Task.WhenAll(tasks).ConfigureAwait(false);
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

		public async Task RemoveAndStopAllAsync(CancellationToken cancel)
		{
			try
			{
				CancelAllInitialization?.Cancel();
				CancelAllInitialization?.Dispose();
			}
			catch (ObjectDisposedException)
			{
				Logger.LogWarning($"{nameof(CancelAllInitialization)} is disposed. This can occur due to an error while processing the wallet.");
			}

			using (await AddRemoveLock.LockAsync(cancel).ConfigureAwait(false))
			{
				List<WalletService> walletsListClone;
				lock (Lock)
				{
					walletsListClone = Wallets.Keys.ToList();
				}
				foreach (var walletService in walletsListClone)
				{
					if (cancel.IsCancellationRequested)
					{
						return;
					}

					walletService.TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
					walletService.ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;

					lock (Lock)
					{
						if (!Wallets.Remove(walletService))
						{
							throw new InvalidOperationException("Wallet service doesn't exist.");
						}
					}

					try
					{
						var keyManager = walletService.KeyManager;
						if (keyManager is { } && WalletDirectories is { })
						{
							string backupWalletFilePath = WalletDirectories.GetWalletBackupPath(Path.GetFileName(keyManager.FilePath));
							keyManager.ToFile(backupWalletFilePath);
							Logger.LogInfo($"{nameof(walletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
						}
						await walletService.StopAsync(cancel).ConfigureAwait(false);
						Logger.LogInfo($"{nameof(WalletService)} is stopped.");
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
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
