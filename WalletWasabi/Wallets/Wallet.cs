using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
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
using WalletWasabi.Exceptions;
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
		private Node _localBitcoinCoreNode = null;
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

			BlockFolderLock = new AsyncLock();
			BlockFolderPath = Path.Combine(dataDir, "Blocks", Network.ToString());
			if (Directory.Exists(BlockFolderPath))
			{
				if (Network == Network.RegTest)
				{
					Directory.Delete(BlockFolderPath, true);
					Directory.CreateDirectory(BlockFolderPath);
				}
			}
			else
			{
				Directory.CreateDirectory(BlockFolderPath);
			}
			KeyManager.AssertCleanKeysIndexed();
			KeyManager.AssertLockedInternalKeysIndexed(14);
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		public event EventHandler<DequeueResult> OnDequeue;

		public static event EventHandler<bool> DownloadingBlockChanged;

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
		public string BlockFolderPath { get; }
		public ServiceConfiguration ServiceConfiguration { get; private set; }
		public string WalletName => KeyManager.WalletName;

		/// <summary>
		/// Unspent Transaction Outputs
		/// </summary>
		public ICoinsView Coins { get; private set; }

		public Network Network { get; }
		public TransactionProcessor TransactionProcessor { get; private set; }

		public Node LocalBitcoinCoreNode
		{
			get
			{
				if (Network == Network.RegTest)
				{
					return Nodes.ConnectedNodes.First();
				}

				return _localBitcoinCoreNode;
			}
			private set => _localBitcoinCoreNode = value;
		}

		public IFeeProvider FeeProvider { get; private set; }
		public CoreNode CoreNode { get; private set; }
		public FilterModel LastProcessedFilter { get; private set; }
		private static Random Random { get; } = new Random();
		private AsyncLock HandleFiltersLock { get; }

		private AsyncLock BlockFolderLock { get; }

		private int NodeTimeouts { get; set; }

		public void RegisterServices(
			BitcoinStore bitcoinStore,
			WasabiSynchronizer syncer,
			NodesGroup nodes,
			ServiceConfiguration serviceConfiguration,
			IFeeProvider feeProvider,
			CoreNode coreNode = null)
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
				CoreNode = coreNode;

				ChaumianClient = new CoinJoinClient(Synchronizer, Network, KeyManager);

				TransactionProcessor = new TransactionProcessor(BitcoinStore.TransactionStore, KeyManager, ServiceConfiguration.DustThreshold, ServiceConfiguration.PrivacyLevelStrong);
				Coins = TransactionProcessor.Coins;

				TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessedAsync;
				ChaumianClient.OnDequeue += ChaumianClient_OnDequeue;

				BitcoinStore.IndexStore.NewFilter += IndexDownloader_NewFilterAsync;
				BitcoinStore.IndexStore.Reorged += IndexDownloader_ReorgedAsync;
				BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;

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

		/// <param name="hash">Block hash of the desired block, represented as a 256 bit integer.</param>
		/// <exception cref="OperationCanceledException"></exception>
		public async Task<Block> FetchBlockAsync(uint256 hash, CancellationToken cancel)
		{
			Block block = await TryGetBlockFromFileAsync(hash, cancel);
			if (block is null)
			{
				block = await DownloadBlockAsync(hash, cancel);
			}
			return block;
		}

		/// <remarks>
		/// Use it at reorgs.
		/// </remarks>
		public async Task DeleteBlockAsync(uint256 hash)
		{
			try
			{
				using (await BlockFolderLock.LockAsync())
				{
					var filePaths = Directory.EnumerateFiles(BlockFolderPath);
					var fileNames = filePaths.Select(Path.GetFileName);
					var hashes = fileNames.Select(x => new uint256(x));

					if (hashes.Contains(hash))
					{
						File.Delete(Path.Combine(BlockFolderPath, hash.ToString()));
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public async Task<int> CountBlocksAsync()
		{
			using (await BlockFolderLock.LockAsync())
			{
				return Directory.EnumerateFiles(BlockFolderPath).Count();
			}
		}

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

				DisconnectDisposeNullLocalBitcoinCoreNode();
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
					await DeleteBlockAsync(invalidBlockHash);
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
			new Height(bestKeyManagerHeight.Value + 1), cancel);
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
				Block currentBlock = await FetchBlockAsync(filterModel.Header.BlockHash, cancel); // Wait until not downloaded.
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

		/// <param name="hash">Block hash of the desired block, represented as a 256 bit integer.</param>
		/// <exception cref="OperationCanceledException"></exception>
		private async Task<Block> TryGetBlockFromFileAsync(uint256 hash, CancellationToken cancel)
		{
			// Try get the block
			Block block = null;
			using (await BlockFolderLock.LockAsync())
			{
				var encoder = new HexEncoder();
				var filePath = Path.Combine(BlockFolderPath, hash.ToString());
				if (File.Exists(filePath))
				{
					try
					{
						var blockBytes = await File.ReadAllBytesAsync(filePath, cancel);
						block = Block.Load(blockBytes, Network);
					}
					catch
					{
						// In case the block file is corrupted and we get an EndOfStreamException exception
						// Ignore any error and continue to re-downloading the block.
						Logger.LogDebug($"Block {hash} file corrupted, deleting file and block will be re-downloaded.");
						File.Delete(filePath);
					}
				}
			}

			return block;
		}

		/// <param name="hash">Block hash of the desired block, represented as a 256 bit integer.</param>
		/// <exception cref="OperationCanceledException"></exception>
		private async Task<Block> DownloadBlockAsync(uint256 hash, CancellationToken cancel)
		{
			Block block = null;
			try
			{
				DownloadingBlockChanged?.Invoke(null, true);

				while (true)
				{
					cancel.ThrowIfCancellationRequested();
					try
					{
						// Try to get block information from local running Core node first.
						block = await TryDownloadBlockFromLocalNodeAsync(hash, cancel);

						if (block != null)
						{
							break;
						}

						// If no connection, wait, then continue.
						while (Nodes.ConnectedNodes.Count == 0)
						{
							await Task.Delay(100, cancel);
						}

						// Select a random node we are connected to.
						Node node = Nodes.ConnectedNodes.RandomElement();
						if (node is null || !node.IsConnected)
						{
							await Task.Delay(100, cancel);
							continue;
						}

						// Download block from selected node.
						try
						{
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(RuntimeParams.Instance.NetworkNodeTimeout))) // 1/2 ADSL	512 kbit/s	00:00:32
							{
								using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancel);
								block = await node.DownloadBlockAsync(hash, lts.Token);
							}

							// Validate block
							if (!block.Check())
							{
								Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.");
								node.DisconnectAsync("Invalid block received.");
								continue;
							}

							if (Nodes.ConnectedNodes.Count > 1) // To minimize risking missing unconfirmed transactions.
							{
								Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}. Block downloaded: {block.GetHash()}.");
								node.DisconnectAsync("Thank you!");
							}

							await NodeTimeoutsAsync(false);
						}
						catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
						{
							Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}, because block download took too long.");

							await NodeTimeoutsAsync(true);

							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);
							Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.");
							node.DisconnectAsync("Block download failed.");
							continue;
						}

						break; // If got this far, then we have the block and it's valid. Break.
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
				}

				// Save the block
				var path = Path.Combine(BlockFolderPath, hash.ToString());
				await SaveBlockToDiskAsync(path, block);
			}
			finally
			{
				DownloadingBlockChanged?.Invoke(null, false);
			}

			return block;
		}

		private async Task SaveBlockToDiskAsync(string path, Block block)
		{
			if (!File.Exists(path))
			{
				using (await BlockFolderLock.LockAsync())
				{
					if (!File.Exists(path))
					{
						await File.WriteAllBytesAsync(path, block.ToBytes());
					}
				}
			}
		}

		private async Task<Block> TryDownloadBlockFromLocalNodeAsync(uint256 hash, CancellationToken cancel)
		{
			if (CoreNode?.RpcClient is null)
			{
				try
				{
					if (LocalBitcoinCoreNode is null || (!LocalBitcoinCoreNode.IsConnected && Network != Network.RegTest)) // If RegTest then we're already connected do not try again.
					{
						DisconnectDisposeNullLocalBitcoinCoreNode();
						using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
						handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
						var nodeConnectionParameters = new NodeConnectionParameters()
						{
							ConnectCancellation = handshakeTimeout.Token,
							IsRelay = false,
							UserAgent = $"/Wasabi:{Constants.ClientVersion}/"
						};

						// If an onion was added must try to use Tor.
						// onlyForOnionHosts should connect to it if it's an onion endpoint automatically and non-Tor endpoints through clearnet/localhost
						if (Synchronizer.WasabiClient.TorClient.IsTorUsed)
						{
							nodeConnectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint, onlyForOnionHosts: true, networkCredential: null, streamIsolation: false));
						}

						var localEndPoint = ServiceConfiguration.BitcoinCoreEndPoint;
						var localNode = await Node.ConnectAsync(Network, localEndPoint, nodeConnectionParameters);
						try
						{
							Logger.LogInfo("TCP Connection succeeded, handshaking...");
							localNode.VersionHandshake(Constants.LocalNodeRequirements, handshakeTimeout.Token);
							var peerServices = localNode.PeerVersion.Services;

							//if (!peerServices.HasFlag(NodeServices.Network) && !peerServices.HasFlag(NodeServices.NODE_NETWORK_LIMITED))
							//{
							//	throw new InvalidOperationException("Wasabi cannot use the local node because it does not provide blocks.");
							//}

							Logger.LogInfo("Handshake completed successfully.");

							if (!localNode.IsConnected)
							{
								throw new InvalidOperationException($"Wasabi could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
									"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
							}
							LocalBitcoinCoreNode = localNode;
						}
						catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested)
						{
							Logger.LogWarning($"Wasabi could not complete the handshake with the local node. Probably Wasabi is not whitelisted by the node.{Environment.NewLine}" +
								"Use \"whitebind\" in the node configuration. (Typically whitebind=127.0.0.1:8333 if Wasabi and the node are on the same machine and whitelist=1.2.3.4 if they are not.)");
							throw;
						}
					}

					// Get Block from local node
					Block blockFromLocalNode = null;
					// Should timeout faster. Not sure if it should ever fail though. Maybe let's keep like this later for remote node connection.
					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(64)))
					{
						blockFromLocalNode = await LocalBitcoinCoreNode.DownloadBlockAsync(hash, cts.Token);
					}

					// Validate retrieved block
					if (!blockFromLocalNode.Check())
					{
						throw new InvalidOperationException("Disconnected node, because invalid block received!");
					}

					// Retrieved block from local node and block is valid
					Logger.LogInfo($"Block acquired from local P2P connection: {hash}.");
					return blockFromLocalNode;
				}
				catch (Exception ex)
				{
					DisconnectDisposeNullLocalBitcoinCoreNode();

					if (ex is SocketException)
					{
						Logger.LogTrace("Did not find local listening and running full node instance. Trying to fetch needed block from other source.");
					}
					else if (ex is OperationCanceledException)
					{
						Logger.LogTrace(ex);
					}
					else
					{
						Logger.LogWarning(ex);
					}
				}
			}
			else
			{
				try
				{
					var block = await CoreNode.RpcClient.GetBlockAsync(hash).ConfigureAwait(false);
					Logger.LogInfo($"Block acquired from RPC connection: {hash}.");
					return block;
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}
			}

			return null;
		}

		private void DisconnectDisposeNullLocalBitcoinCoreNode()
		{
			if (LocalBitcoinCoreNode != null)
			{
				try
				{
					LocalBitcoinCoreNode?.Disconnect();
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
				finally
				{
					try
					{
						LocalBitcoinCoreNode?.Dispose();
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
					finally
					{
						LocalBitcoinCoreNode = null;
						Logger.LogInfo("Local Bitcoin Core node disconnected.");
					}
				}
			}
		}

		private LockTime SelectLockTimeForTransaction()
		{
			var currentTipHeight = Synchronizer.BitcoinStore.SmartHeaderChain.TipHeight;

			return InternalSelectLockTimeForTransaction(currentTipHeight, Random);
		}

		/// <summary>
		/// Current timeout used when downloading a block from the remote node. It is defined in seconds.
		/// </summary>
		private async Task NodeTimeoutsAsync(bool increaseDecrease)
		{
			if (increaseDecrease)
			{
				NodeTimeouts++;
			}
			else
			{
				NodeTimeouts--;
			}

			var timeout = RuntimeParams.Instance.NetworkNodeTimeout;

			// If it times out 2 times in a row then increase the timeout.
			if (NodeTimeouts >= 2)
			{
				NodeTimeouts = 0;
				timeout *= 2;
			}
			else if (NodeTimeouts <= -3) // If it does not time out 3 times in a row, lower the timeout.
			{
				NodeTimeouts = 0;
				timeout = (int)Math.Round(timeout * 0.7);
			}

			// Sanity check
			if (timeout < 32)
			{
				timeout = 32;
			}
			else if (timeout > 600)
			{
				timeout = 600;
			}

			if (timeout == RuntimeParams.Instance.NetworkNodeTimeout)
			{
				return;
			}

			RuntimeParams.Instance.NetworkNodeTimeout = timeout;
			await RuntimeParams.Instance.SaveAsync();

			Logger.LogInfo($"Current timeout value used on block download is: {timeout} seconds.");
		}

		public void SetWaitingForInitState()
		{
			if (State != WalletState.Uninitialized)
			{
				throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized}. Current state: {State}.");
			}
			State = WalletState.WaitingForInit;
		}

		public static Wallet CreateAndRegisterServices(Network network, BitcoinStore bitcoinStore, KeyManager keyManager, WasabiSynchronizer synchronizer, NodesGroup nodes, string dataDir, ServiceConfiguration serviceConfiguration, IFeeProvider feeProvider, CoreNode bitcoinCoreNode = null)
		{
			var wallet = new Wallet(dataDir, network, keyManager);
			wallet.RegisterServices(bitcoinStore, synchronizer, nodes, serviceConfiguration, feeProvider, bitcoinCoreNode);
			return wallet;
		}
	}
}
