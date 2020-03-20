using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets
{
	public class Wallet : BackgroundService
	{
		public Wallet(
			Network network,
			BitcoinStore bitcoinStore,
			KeyManager keyManager,
			WasabiSynchronizer syncer,
			string workFolderDir,
			ServiceConfiguration serviceConfiguration,
			IFeeProvider feeProvider,
			IBlocksProvider blocksProvider)
		{
			Network = Guard.NotNull(nameof(network), network);
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Synchronizer = Guard.NotNull(nameof(syncer), syncer);
			ServiceConfiguration = Guard.NotNull(nameof(serviceConfiguration), serviceConfiguration);
			FeeProvider = Guard.NotNull(nameof(feeProvider), feeProvider);

			ChaumianClient = new CoinJoinClient(Synchronizer, Network, keyManager);
			HandleFiltersLock = new AsyncLock();

			RuntimeParams.SetDataDir(workFolderDir);

			KeyManager.AssertCleanKeysIndexed();
			KeyManager.AssertLockedInternalKeysIndexed(14);

			TransactionProcessor = new TransactionProcessor(BitcoinStore.TransactionStore, KeyManager, ServiceConfiguration.DustThreshold, ServiceConfiguration.PrivacyLevelStrong);
			Coins = TransactionProcessor.Coins;

			TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessedAsync;
			ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;

			BitcoinStore.IndexStore.NewFilter += IndexDownloader_NewFilterAsync;
			BitcoinStore.IndexStore.Reorged += IndexDownloader_ReorgedAsync;
			BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;

			BlocksProvider = blocksProvider;

			State = WalletState.Initialized;
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		public static event EventHandler<bool> InitializingChanged;

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<Block> NewBlockProcessed;

		public WalletState State { get; private set; }
		public BitcoinStore BitcoinStore { get; }
		public KeyManager KeyManager { get; }
		public WasabiSynchronizer Synchronizer { get; }
		public CoinJoinClient ChaumianClient { get; }
		public ServiceConfiguration ServiceConfiguration { get; }
		public string WalletName => KeyManager.WalletName;

		/// <summary>
		/// Unspent Transaction Outputs
		/// </summary>
		public ICoinsView Coins { get; }

		public Network Network { get; }
		public TransactionProcessor TransactionProcessor { get; }

		public IFeeProvider FeeProvider { get; }
		public FilterModel LastProcessedFilter { get; private set; }
		public IBlocksProvider BlocksProvider { get; }

		private static Random Random { get; } = new Random();
		private AsyncLock HandleFiltersLock { get; }

		/// <inheritdoc/>
		public override async Task StartAsync(CancellationToken cancel)
		{
			var prevState = State;
			try
			{
				State = WalletState.Starting;
				InitializingChanged?.Invoke(this, true);

				if (!Synchronizer.IsRunning)
				{
					throw new NotSupportedException($"{nameof(Synchronizer)} is not running.");
				}

				while (!BitcoinStore.IsInitialized)
				{
					await Task.Delay(100).ConfigureAwait(false);

					cancel.ThrowIfCancellationRequested();
				}

				using (BenchmarkLogger.Measure())
				{
					await RuntimeParams.LoadAsync().ConfigureAwait(false);

					ChaumianClient.Start();

					using (await HandleFiltersLock.LockAsync().ConfigureAwait(false))
					{
						await LoadWalletStateAsync(cancel).ConfigureAwait(false);
						await LoadDummyMempoolAsync().ConfigureAwait(false);
					}

					if (KeyManager.FilePath is { })
					{
						// Set the LastAccessTime.
						new FileInfo(KeyManager.FilePath)
						{
							LastAccessTime = DateTime.Now
						};
					}
				}

				await base.StartAsync(cancel).ConfigureAwait(false);

				State = WalletState.Started;
			}
			catch
			{
				State = prevState;
				throw;
			}
			finally
			{
				InitializingChanged?.Invoke(this, false);
			}
		}

		/// <inheritdoc />
		protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		/// <param name="allowedInputs">Only these inputs allowed to be used to build the transaction. The wallet must know the corresponding private keys.</param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public BuildTransactionResult BuildTransaction(
			string password,
			PaymentIntent payments,
			FeeStrategy feeStrategy,
			bool allowUnconfirmed = false,
			IEnumerable<OutPoint> allowedInputs = null)
		{
			var builder = new TransactionFactory(Network, KeyManager, Coins, password, allowUnconfirmed);
			return builder.BuildTransaction(
				payments,
				() =>
				{
					if (feeStrategy.Type == FeeStrategyType.Target)
					{
						return FeeProvider.AllFeeEstimate?.GetFeeRate(feeStrategy.Target) ?? throw new InvalidOperationException("Cannot get fee estimations.");
					}
					else if (feeStrategy.Type == FeeStrategyType.Rate)
					{
						return feeStrategy.Rate;
					}
					else
					{
						throw new NotSupportedException(feeStrategy.Type.ToString());
					}
				},
				allowedInputs,
				SelectLockTimeForTransaction);
		}

		public void RenameLabel(SmartCoin coin, SmartLabel newLabel)
		{
			coin.Label = newLabel ?? SmartLabel.Empty;
			var key = KeyManager.GetKeys(x => x.P2wpkhScript == coin.ScriptPubKey).SingleOrDefault();
			if (key != null)
			{
				key.SetLabel(coin.Label, KeyManager);
			}
		}

		public ISet<string> GetLabels() => TransactionProcessor.Coins.AsAllCoinsView()
			.SelectMany(x => x.Label.Labels)
			.Concat(KeyManager
				.GetKeys()
				.SelectMany(x => x.Label.Labels))
			.ToHashSet();

		/// <inheritdoc/>
		public async override Task StopAsync(CancellationToken cancel)
		{
			try
			{
				var prevState = State;
				State = WalletState.Stopping;

				await base.StopAsync(cancel).ConfigureAwait(false);

				if (prevState >= WalletState.Initialized)
				{
					BitcoinStore.IndexStore.NewFilter -= IndexDownloader_NewFilterAsync;
					BitcoinStore.IndexStore.Reorged -= IndexDownloader_ReorgedAsync;
					BitcoinStore.MempoolService.TransactionReceived -= Mempool_TransactionReceived;
					TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessedAsync;
					ChaumianClient.OnDequeue -= ChaumianClient_OnDequeue;

					await ChaumianClient.StopAsync(cancel).ConfigureAwait(false);
				}

				Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.");
			}
			finally
			{
				State = WalletState.Stopped;
			}
		}

		internal static LockTime InternalSelectLockTimeForTransaction(uint tipHeight, Random rnd)
		{
			try
			{
				// We use the timelock distribution observed in the bitcoin network
				// in order to reduce the wasabi wallet transactions fingerprinting
				// chances.
				//
				// Network observations:
				// 90.0% uses locktime = 0
				//  7.5% uses locktime = current tip
				//  0.65% uses locktime = next tip (current tip + 1)
				//  1.85% uses up to 5 blocks in the future (we don't do this)
				//  0.65% uses an uniform random from -1 to -99

				// sometimes pick locktime a bit further back, to help privacy.
				var randomValue = rnd.NextDouble();
				return randomValue switch
				{
					var r when r < (0.9) => LockTime.Zero,
					var r when r < (0.9 + 0.075) => tipHeight,
					var r when r < (0.9 + 0.075 + 0.0065) => (uint)(tipHeight + 1),
					_ => (uint)(tipHeight - rnd.Next(1, 100))
				};
			}
			catch
			{
				return LockTime.Zero;
			}
		}

		private async void TransactionProcessor_WalletRelevantTransactionProcessedAsync(object sender, ProcessedResult e)
		{
			try
			{
				foreach (var coin in e.NewlySpentCoins.Concat(e.ReplacedCoins).Concat(e.SuccessfullyDoubleSpentCoins).Distinct())
				{
					ChaumianClient.ExposedLinks.TryRemove(coin.OutPoint, out _);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}

			try
			{
				IEnumerable<SmartCoin> newCoins = e.NewlyReceivedCoins.Concat(e.RestoredCoins).Distinct();
				if (newCoins.Any())
				{
					if (ChaumianClient.State.Contains(e.Transaction.Transaction.Inputs.Select(x => x.PrevOut)))
					{
						var coinsToQueue = new HashSet<SmartCoin>();
						foreach (var newCoin in newCoins)
						{
							// If it's being mixed and anonset is not sufficient, then queue it.
							if (newCoin.Unspent && ChaumianClient.HasIngredients
								&& newCoin.AnonymitySet < ServiceConfiguration.MixUntilAnonymitySet)
							{
								coinsToQueue.Add(newCoin);
							}
						}

						await ChaumianClient.QueueCoinsToMixAsync(coinsToQueue).ConfigureAwait(false);
					}
				}

				WalletRelevantTransactionProcessed?.Invoke(this, e);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private void ChaumianClient_OnDequeue(object sender, DequeueResult e)
		{
			OnDequeue?.Invoke(this, e);
		}

		private void Mempool_TransactionReceived(object sender, SmartTransaction tx)
		{
			try
			{
				TransactionProcessor.Process(tx);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private async void IndexDownloader_ReorgedAsync(object sender, FilterModel invalidFilter)
		{
			try
			{
				using (await HandleFiltersLock.LockAsync())
				{
					uint256 invalidBlockHash = invalidFilter.Header.BlockHash;
					if (BlocksProvider is CachedBlocksProvider blockCache)
					{
						await blockCache.InvalidateAsync(invalidBlockHash, CancellationToken.None).ConfigureAwait(false);
					}
					KeyManager.SetMaxBestHeight(new Height(invalidFilter.Header.Height - 1));
					TransactionProcessor.UndoBlock((int)invalidFilter.Header.Height);
					BitcoinStore.TransactionStore.ReleaseToMempoolFromBlock(invalidBlockHash);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private async void IndexDownloader_NewFilterAsync(object sender, FilterModel filterModel)
		{
			try
			{
				using (await HandleFiltersLock.LockAsync())
				{
					if (KeyManager.GetBestHeight() < filterModel.Header.Height)
					{
						await ProcessFilterModelAsync(filterModel, CancellationToken.None);
					}
				}
				NewFilterProcessed?.Invoke(this, filterModel);

				do
				{
					await Task.Delay(100);
					if (Synchronizer is null || BitcoinStore?.SmartHeaderChain is null)
					{
						return;
					}
					// Make sure fully synced and this filter is the latest filter.
					if (BitcoinStore.SmartHeaderChain.HashesLeft != 0 || BitcoinStore.SmartHeaderChain.TipHash != filterModel.Header.BlockHash)
					{
						return;
					}
				} while (Synchronizer.AreRequestsBlocked()); // If requests are blocked, delay mempool cleanup, because coinjoin answers are always priority.

				await BitcoinStore.MempoolService?.TryPerformMempoolCleanupAsync(Synchronizer?.WasabiClient?.TorClient?.DestinationUriAction, Synchronizer?.WasabiClient?.TorClient?.TorSocks5EndPoint);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private async Task LoadWalletStateAsync(CancellationToken cancel)
		{
			KeyManager.AssertNetworkOrClearBlockState(Network);
			Height bestKeyManagerHeight = KeyManager.GetBestHeight();

			TransactionProcessor.Process(BitcoinStore.TransactionStore.ConfirmedStore.GetTransactions().TakeWhile(x => x.Height <= bestKeyManagerHeight));

			// Go through the filters and queue to download the matches.
			await BitcoinStore.IndexStore.ForeachFiltersAsync(async (filterModel) =>
			{
				if (filterModel.Filter != null) // Filter can be null if there is no bech32 tx.
				{
					await ProcessFilterModelAsync(filterModel, cancel);
				}
			},
			new Height(bestKeyManagerHeight.Value + 1));
		}

		private async Task LoadDummyMempoolAsync()
		{
			if (BitcoinStore.TransactionStore.MempoolStore.IsEmpty())
			{
				return;
			}

			// Only clean the mempool if we're fully synchronized.
			if (BitcoinStore.SmartHeaderChain.HashesLeft == 0)
			{
				try
				{
					using var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint);
					var compactness = 10;

					var mempoolHashes = await client.GetMempoolHashesAsync(compactness);

					var txsToProcess = new List<SmartTransaction>();
					foreach (var tx in BitcoinStore.TransactionStore.MempoolStore.GetTransactions())
					{
						uint256 hash = tx.GetHash();
						if (mempoolHashes.Contains(hash.ToString().Substring(0, compactness)))
						{
							txsToProcess.Add(tx);
							Logger.LogInfo($"Transaction was successfully tested against the backend's mempool hashes: {hash}.");
						}
						else
						{
							BitcoinStore.TransactionStore.MempoolStore.TryRemove(tx.GetHash(), out _);
						}
					}

					TransactionProcessor.Process(txsToProcess);
				}
				catch (Exception ex)
				{
					// When there's a connection failure do not clean the transactions, add them to processing.
					TransactionProcessor.Process(BitcoinStore.TransactionStore.MempoolStore.GetTransactions());

					Logger.LogWarning(ex);
				}
			}
			else
			{
				TransactionProcessor.Process(BitcoinStore.TransactionStore.MempoolStore.GetTransactions());
			}
		}

		private async Task ProcessFilterModelAsync(FilterModel filterModel, CancellationToken cancel)
		{
			var matchFound = filterModel.Filter.MatchAny(KeyManager.GetPubKeyScriptBytes(), filterModel.FilterKey);
			if (matchFound)
			{
				Block currentBlock = await BlocksProvider.GetBlockAsync(filterModel.Header.BlockHash, cancel); // Wait until not downloaded.
				var height = new Height(filterModel.Header.Height);

				var txsToProcess = new List<SmartTransaction>();
				for (int i = 0; i < currentBlock.Transactions.Count; i++)
				{
					Transaction tx = currentBlock.Transactions[i];
					txsToProcess.Add(new SmartTransaction(tx, height, currentBlock.GetHash(), i, firstSeen: currentBlock.Header.BlockTime));
				}
				TransactionProcessor.Process(txsToProcess);
				KeyManager.SetBestHeight(height);

				NewBlockProcessed?.Invoke(this, currentBlock);
			}

			LastProcessedFilter = filterModel;
		}


		private LockTime SelectLockTimeForTransaction()
		{
			var currentTipHeight = Synchronizer.BitcoinStore.SmartHeaderChain.TipHeight;

			return InternalSelectLockTimeForTransaction(currentTipHeight, Random);
		}
	}
}
