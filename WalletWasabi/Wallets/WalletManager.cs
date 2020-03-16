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
			using (BenchmarkLogger.Measure())
			{
				Wallets = new Dictionary<SmartWallet, HashSet<uint256>>();
				Lock = new object();
				AddRemoveLock = new AsyncLock();
				CancelAllInitialization = new CancellationTokenSource();
				WalletDirectories = walletDirectories;

				if (WalletDirectories is { })
				{
					foreach (var walletFile in WalletDirectories.EnumerateWalletFiles())
					{
						try
						{
							SmartWallet wallet = new SmartWallet(walletFile.FullName);
							Wallets.Add(wallet, new HashSet<uint256>());

							wallet.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
							wallet.OnDequeue += ChaumianClient_OnDequeue;
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex);
						}
					}
				}
			}
		}

		private CancellationTokenSource CancelAllInitialization { get; }

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		private Dictionary<SmartWallet, HashSet<uint256>> Wallets { get; }
		private object Lock { get; }
		private AsyncLock AddRemoveLock { get; }

		private BitcoinStore BitcoinStore { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private NodesGroup Nodes { get; set; }
		private string DataDir { get; set; }
		private ServiceConfiguration ServiceConfiguration { get; set; }

		public void SignalQuitPending(bool isQuitPending)
		{
			lock (Lock)
			{
				foreach (var client in Wallets.Keys.Where(x => x.Wallet is { }).Select(x => x.Wallet.ChaumianClient))
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
				return Wallets.Keys.FirstOrDefault(x => x.Wallet is { })?.Wallet;
			}
		}

		public bool AnyWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.Any(x => x.Wallet is { });
			}
		}

		public async Task<SmartWallet> CreateAndStartWalletServiceAsync(KeyManager keyManager)
		{
			using (await AddRemoveLock.LockAsync(CancelAllInitialization.Token).ConfigureAwait(false))
			{
				var wallet = Wallets.Single(x => x.Key.KeyManager == keyManager).Key;
				if (wallet.IsAlive)
				{
					return wallet;
				}

				try
				{
					wallet.InitializeWalletService(BitcoinStore, Synchronizer, Nodes, DataDir, ServiceConfiguration, FeeProvider, BitcoinCoreNode);

					var cancel = CancelAllInitialization.Token;

					Logger.LogInfo($"Starting {nameof(WalletService)}...");
					await wallet.StartAsync(cancel).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(WalletService)} started.");

					cancel.ThrowIfCancellationRequested();

					return wallet;
				}
				catch
				{
					await wallet.StopAsync(CancellationToken.None).ConfigureAwait(false);
					throw;
				}
			}
		}

		public async Task DequeueAllCoinsGracefullyAsync(DequeueReason reason, CancellationToken token)
		{
			IEnumerable<Task> tasks = null;
			lock (Lock)
			{
				tasks = Wallets.Keys.Where(x => x.Wallet is { }).Select(x => x.Wallet.ChaumianClient.DequeueAllCoinsFromMixGracefullyAsync(reason, token)).ToArray();
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
				List<SmartWallet> walletsListClone;
				lock (Lock)
				{
					walletsListClone = Wallets.Keys.ToList();
				}
				foreach (var wallet in walletsListClone)
				{
					if (cancel.IsCancellationRequested)
					{
						return;
					}

					wallet.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
					wallet.OnDequeue -= ChaumianClient_OnDequeue;

					lock (Lock)
					{
						if (!Wallets.Remove(wallet))
						{
							throw new InvalidOperationException("Wallet service doesn't exist.");
						}
					}

					try
					{
						var keyManager = wallet.KeyManager;
						if (keyManager is { } && WalletDirectories is { })
						{
							string backupWalletFilePath = WalletDirectories.GetWalletFilePaths(Path.GetFileName(keyManager.FilePath)).walletBackupFilePath;
							keyManager.ToFile(backupWalletFilePath);
							Logger.LogInfo($"{nameof(wallet.KeyManager)} backup saved to `{backupWalletFilePath}`.");
						}
						await wallet.StopAsync(cancel).ConfigureAwait(false);
						wallet?.Dispose();
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
				foreach (var pair in Wallets.Where(x => x.Key.IsAlive && !x.Value.Contains(tx.GetHash())))
				{
					var wallet = pair.Key;
					pair.Value.Add(tx.GetHash());
					wallet.Wallet.TransactionProcessor.Process(tx);
				}
			}
		}

		public void Process(SmartTransaction transaction)
		{
			lock (Lock)
			{
				foreach (var wallet in Wallets.Where(x => x.Key.IsAlive))
				{
					wallet.Key.Wallet.TransactionProcessor.Process(transaction);
				}
			}
		}

		public IEnumerable<SmartCoin> CoinsByOutPoint(OutPoint input)
		{
			lock (Lock)
			{
				var res = new List<SmartCoin>();
				foreach (var walletService in Wallets.Where(x => x.Key.IsAlive))
				{
					SmartCoin coin = walletService.Key.Wallet.Coins.GetByOutPoint(input);
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
				foreach (var pair in Wallets.Where(x => x.Key.IsAlive))
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
