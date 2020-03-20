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
		public WalletManager(Network network, WalletDirectories walletDirectories)
		{
			using (BenchmarkLogger.Measure())
			{
				Network = Guard.NotNull(nameof(network), network);
				WalletDirectories = Guard.NotNull(nameof(walletDirectories), walletDirectories);
				Wallets = new Dictionary<Wallet, HashSet<uint256>>();
				Lock = new object();
				AddRemoveLock = new AsyncLock();
				CancelAllInitialization = new CancellationTokenSource();
			}
		}

		private CancellationTokenSource CancelAllInitialization { get; }

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		private Dictionary<Wallet, HashSet<uint256>> Wallets { get; }
		private object Lock { get; }
		private AsyncLock AddRemoveLock { get; }

		private BitcoinStore BitcoinStore { get; set; }
		private WasabiSynchronizer Synchronizer { get; set; }
		private NodesGroup Nodes { get; set; }
		private ServiceConfiguration ServiceConfiguration { get; set; }

		private IBlocksProvider BlocksProvider { get; set; }

		public void SignalQuitPending(bool isQuitPending)
		{
			lock (Lock)
			{
				foreach (var client in Wallets.Keys.Where(x => x.ChaumianClient is { }).Select(x => x.ChaumianClient))
				{
					client.IsQuitPending = isQuitPending;
				}
			}
		}

		private IFeeProvider FeeProvider { get; set; }
		private CoreNode BitcoinCoreNode { get; set; }
		public Network Network { get; }
		public WalletDirectories WalletDirectories { get; }

		public Wallet GetFirstOrDefaultWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.FirstOrDefault(x => x.State == WalletState.Started);
			}
		}

		public bool AnyWallet()
		{
			lock (Lock)
			{
				return Wallets.Keys.Any(x => x.State >= WalletState.Starting);
			}
		}

		public async Task<Wallet> CreateAndStartWalletAsync(KeyManager keyManager)
		{
			using (await AddRemoveLock.LockAsync(CancelAllInitialization.Token).ConfigureAwait(false))
			{
				Wallet wallet = null;
				try
				{
					wallet = new Wallet(Network, BitcoinStore, keyManager, Synchronizer, WalletDirectories.WorkDir, ServiceConfiguration, FeeProvider, BlocksProvider);

					var cancel = CancelAllInitialization.Token;
					lock (Lock)
					{
						Wallets.Add(wallet, new HashSet<uint256>());
					}

					Logger.LogInfo($"Starting {nameof(Wallet)}...");
					await wallet.StartAsync(cancel).ConfigureAwait(false);
					Logger.LogInfo($"{nameof(Wallet)} started.");

					cancel.ThrowIfCancellationRequested();

					wallet.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
					wallet.OnDequeue += ChaumianClient_OnDequeue;

					return wallet;
				}
				catch
				{
					if (wallet is { })
					{
						wallet.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
						wallet.OnDequeue -= ChaumianClient_OnDequeue;
						lock (Lock)
						{
							Wallets.Remove(wallet);
						}
						await wallet.StopAsync(CancellationToken.None).ConfigureAwait(false);
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
				tasks = Wallets.Keys.Where(x => x.ChaumianClient is { }).Select(x => x.ChaumianClient.DequeueAllCoinsFromMixGracefullyAsync(reason, token)).ToArray();
			}
			await Task.WhenAll(tasks).ConfigureAwait(false);
		}

		private void ChaumianClient_OnDequeue(object sender, DequeueResult e)
		{
			OnDequeue?.Invoke(sender, e);
		}

		private void TransactionProcessor_WalletRelevantTransactionProcessed(object sender, ProcessedResult e)
		{
			WalletRelevantTransactionProcessed?.Invoke(sender, e);
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
				List<Wallet> walletsListClone;
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
						Logger.LogInfo($"{nameof(Wallet)} is stopped.");
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
				foreach (var pair in Wallets.Where(x => x.Key.State == WalletState.Started && !x.Value.Contains(tx.GetHash())))
				{
					var wallet = pair.Key;
					pair.Value.Add(tx.GetHash());
					wallet.TransactionProcessor.Process(tx);
				}
			}
		}

		public void Process(SmartTransaction transaction)
		{
			lock (Lock)
			{
				foreach (var wallet in Wallets.Where(x => x.Key.State == WalletState.Started))
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
				foreach (var wallet in Wallets.Where(x => x.Key.State == WalletState.Started))
				{
					SmartCoin coin = wallet.Key.Coins.GetByOutPoint(input);
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
				foreach (var pair in Wallets.Where(x => x.Key.State == WalletState.Started))
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

		public void RegisterServices(BitcoinStore bitcoinStore, WasabiSynchronizer synchronizer, NodesGroup nodes, ServiceConfiguration serviceConfiguration, IFeeProvider feeProvider, CoreNode bitcoinCoreNode, IBlocksProvider blocksProvider)
		{
			BitcoinStore = bitcoinStore;
			Synchronizer = synchronizer;
			Nodes = nodes;
			ServiceConfiguration = serviceConfiguration;
			FeeProvider = feeProvider;
			BitcoinCoreNode = bitcoinCoreNode;
			BlocksProvider = blocksProvider;
		}
	}
}
