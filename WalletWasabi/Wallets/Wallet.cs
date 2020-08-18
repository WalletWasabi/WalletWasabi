using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
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
using WalletWasabi.WebClients.PayJoin;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets
{
	public class Wallet : BackgroundService
	{
		private WalletState _state;

		public Wallet(string dataDir, Network network, string filePath) : this(dataDir, network, KeyManager.FromFile(filePath))
		{
		}

		public Wallet(string dataDir, Network network, KeyManager keyManager)
		{
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);

			RuntimeParams.SetDataDir(dataDir);
			HandleFiltersLock = new AsyncLock();

			KeyManager.AssertCleanKeysIndexed();
			KeyManager.AssertLockedInternalKeysIndexed(14);
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		public static event EventHandler<bool> InitializingChanged;

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<Block> NewBlockProcessed;

		public event EventHandler<WalletState> StateChanged;

		public WalletState State
		{
			get => _state;
			private set
			{
				if (_state == value)
				{
					return;
				}
				_state = value;
				StateChanged?.Invoke(this, _state);
			}
		}

		public string DataDir { get; }
		public BitcoinStore BitcoinStore { get; private set; }
		public KeyManager KeyManager { get; }
		public WasabiSynchronizer Synchronizer { get; private set; }
		public CoinJoinClient ChaumianClient { get; private set; }
		public NodesGroup Nodes { get; private set; }
		public ServiceConfiguration ServiceConfiguration { get; private set; }
		public string WalletName => KeyManager.WalletName;

		/// <summary>
		/// Unspent Transaction Outputs
		/// </summary>
		public ICoinsView Coins { get; private set; }

		public Network Network { get; }
		public TransactionProcessor TransactionProcessor { get; private set; }

		public IFeeProvider FeeProvider { get; private set; }
		public FilterModel LastProcessedFilter { get; private set; }
		public IBlockProvider BlockProvider { get; private set; }
		private AsyncLock HandleFiltersLock { get; }

		public void RegisterServices(
			BitcoinStore bitcoinStore,
			WasabiSynchronizer syncer,
			NodesGroup nodes,
			ServiceConfiguration serviceConfiguration,
			IFeeProvider feeProvider,
			IBlockProvider blockProvider)
		{
			if (State > WalletState.WaitingForInit)
			{
				throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized} or {WalletState.WaitingForInit}. Current state: {State}.");
			}

			try
			{
				BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
				Nodes = Guard.NotNull(nameof(nodes), nodes);
				Synchronizer = Guard.NotNull(nameof(syncer), syncer);
				ServiceConfiguration = Guard.NotNull(nameof(serviceConfiguration), serviceConfiguration);
				FeeProvider = Guard.NotNull(nameof(feeProvider), feeProvider);

				ChaumianClient = new CoinJoinClient(Synchronizer, Network, KeyManager);

				TransactionProcessor = new TransactionProcessor(BitcoinStore.TransactionStore, KeyManager, ServiceConfiguration.DustThreshold, ServiceConfiguration.PrivacyLevelStrong);
				Coins = TransactionProcessor.Coins;

				TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessedAsync;
				ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;

				BitcoinStore.IndexStore.NewFilter += IndexDownloader_NewFilterAsync;
				BitcoinStore.IndexStore.Reorged += IndexDownloader_ReorgedAsync;
				BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;

				BlockProvider = blockProvider;

				State = WalletState.Initialized;
			}
			catch
			{
				State = WalletState.Uninitialized;
				throw;
			}
		}

		/// <inheritdoc/>
		public override async Task StartAsync(CancellationToken cancel)
		{
			if (State != WalletState.Initialized)
			{
				throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Initialized}. Current state: {State}.");
			}

			try
			{
				State = WalletState.Starting;
				InitializingChanged?.Invoke(this, true);

				KeyManager.SetLastAccessTimeForNow();

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
				}

				await base.StartAsync(cancel).ConfigureAwait(false);

				State = WalletState.Started;
			}
			catch
			{
				State = WalletState.Initialized;
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
			IEnumerable<OutPoint> allowedInputs = null,
			IPayjoinClient payjoinClient = null)
		{
			var builder = new TransactionFactory(Network, KeyManager, Coins, BitcoinStore.TransactionStore, password, allowUnconfirmed);
			return builder.BuildTransaction(
				payments,
				feeRateFetcher: () =>
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
				lockTimeSelector: () =>
				{
					var currentTipHeight = Synchronizer.BitcoinStore.SmartHeaderChain.TipHeight;
					return LockTimeSelector.Instance.GetLockTimeBasedOnDistribution(currentTipHeight);
				},
				payjoinClient);
		}

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
					Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.");
				}
			}
			finally
			{
				State = WalletState.Stopped;
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
								&& newCoin.AnonymitySet < ServiceConfiguration.GetMixUntilAnonymitySetValue())
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
				using (await HandleFiltersLock.LockAsync().ConfigureAwait(false))
				{
					uint256 invalidBlockHash = invalidFilter.Header.BlockHash;
					if (BlockProvider is CachedBlockProvider blockCache)
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
				using (await HandleFiltersLock.LockAsync().ConfigureAwait(false))
				{
					if (KeyManager.GetBestHeight() < filterModel.Header.Height)
					{
						await ProcessFilterModelAsync(filterModel, CancellationToken.None).ConfigureAwait(false);
					}
				}
				NewFilterProcessed?.Invoke(this, filterModel);

				do
				{
					await Task.Delay(100).ConfigureAwait(false);
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

				var task = BitcoinStore.MempoolService?.TryPerformMempoolCleanupAsync(Synchronizer?.WasabiClient?.TorClient?.DestinationUriAction, Synchronizer?.WasabiClient?.TorClient?.TorSocks5EndPoint);

				if (task is { })
				{
					await task.ConfigureAwait(false);
				}
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
					await ProcessFilterModelAsync(filterModel, cancel).ConfigureAwait(false);
				}
			},
			new Height(bestKeyManagerHeight.Value + 1), cancel).ConfigureAwait(false);
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

					var mempoolHashes = await client.GetMempoolHashesAsync(compactness).ConfigureAwait(false);

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
				Block currentBlock = await BlockProvider.GetBlockAsync(filterModel.Header.BlockHash, cancel).ConfigureAwait(false); // Wait until not downloaded.
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

		public void SetWaitingForInitState()
		{
			if (State != WalletState.Uninitialized)
			{
				throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized}. Current state: {State}.");
			}
			State = WalletState.WaitingForInit;
		}

		public static Wallet CreateAndRegisterServices(Network network, BitcoinStore bitcoinStore, KeyManager keyManager, WasabiSynchronizer synchronizer, NodesGroup nodes, string dataDir, ServiceConfiguration serviceConfiguration, IFeeProvider feeProvider, IBlockProvider blockProvider)
		{
			var wallet = new Wallet(dataDir, network, keyManager);
			wallet.RegisterServices(bitcoinStore, synchronizer, nodes, serviceConfiguration, feeProvider, blockProvider);
			return wallet;
		}
	}
}
