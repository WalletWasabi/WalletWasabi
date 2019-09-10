using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.DataEncoders;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.TransactionBuilding;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class WalletService
	{
		public static event EventHandler<bool> DownloadingBlockChanged;

		// So we will make sure when blocks are downloading with multiple wallet services, they do not conflict.
		private static AsyncLock BlockDownloadLock { get; } = new AsyncLock();

		private static bool DownloadingBlockBacking;

		public static bool DownloadingBlock
		{
			get => DownloadingBlockBacking;
			set
			{
				if (value != DownloadingBlockBacking)
				{
					DownloadingBlockBacking = value;
					DownloadingBlockChanged?.Invoke(null, value);
				}
			}
		}

		public BitcoinStore BitcoinStore { get; }
		public KeyManager KeyManager { get; }
		public WasabiSynchronizer Synchronizer { get; }
		public CcjClient ChaumianClient { get; }
		public MempoolService Mempool { get; }
		public NodesGroup Nodes { get; }
		public string BlocksFolderPath { get; }
		public string TransactionsFolderPath { get; }
		public string TransactionsFilePath { get; }

		private AsyncLock HandleFiltersLock { get; }

		private AsyncLock BlockFolderLock { get; }

		private int NodeTimeouts { get; set; }

		public ServiceConfiguration ServiceConfiguration { get; }

		public ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)> ProcessedBlocks { get; }

		public ObservableConcurrentHashSet<SmartCoin> Coins { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<Block> NewBlockProcessed;

		public Network Network => Synchronizer.Network;

		public TransactionProcessor TransactionProcessor { get; }

		private ConcurrentHashSet<SmartTransaction> TransactionCache { get; }

		public WalletService(
			BitcoinStore bitcoinStore,
			KeyManager keyManager,
			WasabiSynchronizer syncer,
			CcjClient chaumianClient,
			MempoolService mempool,
			NodesGroup nodes,
			string workFolderDir,
			ServiceConfiguration serviceConfiguration)
		{
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			Synchronizer = Guard.NotNull(nameof(syncer), syncer);
			ChaumianClient = Guard.NotNull(nameof(chaumianClient), chaumianClient);
			Mempool = Guard.NotNull(nameof(mempool), mempool);
			ServiceConfiguration = Guard.NotNull(nameof(serviceConfiguration), serviceConfiguration);

			ProcessedBlocks = new ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)>();
			HandleFiltersLock = new AsyncLock();

			Coins = new ObservableConcurrentHashSet<SmartCoin>();

			BlocksFolderPath = Path.Combine(workFolderDir, "Blocks", Network.ToString());
			TransactionsFolderPath = Path.Combine(workFolderDir, "Transactions", Network.ToString());
			RuntimeParams.SetDataDir(workFolderDir);

			BlockFolderLock = new AsyncLock();

			KeyManager.AssertCleanKeysIndexed();
			KeyManager.AssertLockedInternalKeysIndexed(14);

			TransactionCache = new ConcurrentHashSet<SmartTransaction>();

			TransactionProcessor = new TransactionProcessor(KeyManager, Mempool.TransactionHashes, Coins, ServiceConfiguration.DustThreshold, TransactionCache);
			TransactionProcessor.CoinSpent += TransactionProcessor_CoinSpent;
			TransactionProcessor.CoinReceived += TransactionProcessor_CoinReceivedAsync;

			if (Directory.Exists(BlocksFolderPath))
			{
				if (Synchronizer.Network == Network.RegTest)
				{
					Directory.Delete(BlocksFolderPath, true);
					Directory.CreateDirectory(BlocksFolderPath);
				}
			}
			else
			{
				Directory.CreateDirectory(BlocksFolderPath);
			}

			if (Directory.Exists(TransactionsFolderPath))
			{
				if (Synchronizer.Network == Network.RegTest)
				{
					Directory.Delete(TransactionsFolderPath, true);
					Directory.CreateDirectory(TransactionsFolderPath);
				}
			}
			else
			{
				Directory.CreateDirectory(TransactionsFolderPath);
			}

			var walletName = "UnnamedWallet";
			if (!string.IsNullOrWhiteSpace(KeyManager.FilePath))
			{
				walletName = Path.GetFileNameWithoutExtension(KeyManager.FilePath);
			}
			TransactionsFilePath = Path.Combine(TransactionsFolderPath, $"{walletName}Transactions.json");

			BitcoinStore.IndexStore.NewFilter += IndexDownloader_NewFilterAsync;
			BitcoinStore.IndexStore.Reorged += IndexDownloader_ReorgedAsync;
			Mempool.TransactionReceived += Mempool_TransactionReceivedAsync;
		}

		private void TransactionProcessor_CoinSpent(object sender, SmartCoin spentCoin)
		{
			ChaumianClient.ExposedLinks.TryRemove(spentCoin.GetTxoRef(), out _);
		}

		private async void TransactionProcessor_CoinReceivedAsync(object sender, SmartCoin newCoin)
		{
			// If it's being mixed and anonset is not sufficient, then queue it.
			if (newCoin.Unspent && ChaumianClient.HasIngredients
				&& newCoin.AnonymitySet < ServiceConfiguration.MixUntilAnonymitySet
				&& ChaumianClient.State.Contains(newCoin.SpentOutputs))
			{
				try
				{
					await ChaumianClient.QueueCoinsToMixAsync(newCoin);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}
			}
		}

		private void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				foreach (var toRemove in e.OldItems.Cast<SmartCoin>())
				{
					if (toRemove.SpenderTransactionId != null)
					{
						foreach (var toAlsoRemove in Coins.Where(x => x.TransactionId == toRemove.SpenderTransactionId).Distinct().ToList())
						{
							Coins.TryRemove(toAlsoRemove);
						}
					}

					Mempool.TransactionHashes.TryRemove(toRemove.TransactionId);
					var txToRemove = TryGetTxFromCache(toRemove.TransactionId);
					if (txToRemove != default(SmartTransaction))
					{
						TransactionCache.TryRemove(txToRemove);
					}
				}
			}

			RefreshCoinHistories();
		}

		private static AsyncLock TransactionProcessingLock { get; } = new AsyncLock();

		private async void Mempool_TransactionReceivedAsync(object sender, SmartTransaction tx)
		{
			try
			{
				using (await TransactionProcessingLock.LockAsync())
				{
					if (await ProcessTransactionAsync(tx))
					{
						SerializeTransactionCache();
					}
				}
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
					uint256 invalidBlockHash = invalidFilter.BlockHash;
					await DeleteBlockAsync(invalidBlockHash);
					var blockState = KeyManager.TryRemoveBlockState(invalidBlockHash);
					ProcessedBlocks.TryRemove(invalidBlockHash, out _);
					if (blockState != null && blockState.BlockHeight != default(Height))
					{
						foreach (var toRemove in Coins.Where(x => x.Height == blockState.BlockHeight).Distinct().ToList())
						{
							Coins.TryRemove(toRemove);
						}
					}
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
					if (filterModel.Filter != null && !KeyManager.CointainsBlockState(filterModel.BlockHash))
					{
						await ProcessFilterModelAsync(filterModel, CancellationToken.None);
					}
				}
				NewFilterProcessed?.Invoke(this, filterModel);

				do
				{
					await Task.Delay(100);
					if (Synchronizer is null || BitcoinStore?.HashChain is null)
					{
						return;
					}
					// Make sure fully synced and this filter is the lastest filter.
					if (BitcoinStore.HashChain.HashesLeft != 0 || BitcoinStore.HashChain.TipHash != filterModel.BlockHash)
					{
						return;
					}
				} while (Synchronizer.AreRequestsBlocked()); // If requests are blocked, delay mempool cleanup, because coinjoin answers are always priority.

				await Mempool?.TryPerformMempoolCleanupAsync(Synchronizer?.WasabiClient?.TorClient?.DestinationUriAction, Synchronizer?.WasabiClient?.TorClient?.TorSocks5EndPoint);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public async Task InitializeAsync(CancellationToken cancel)
		{
			if (!Synchronizer.IsRunning)
			{
				throw new NotSupportedException($"{nameof(Synchronizer)} is not running.");
			}

			await RuntimeParams.LoadAsync();

			using (await HandleFiltersLock.LockAsync())
			{
				var unconfirmedTransactions = new SmartTransaction[0];
				var confirmedTransactions = new SmartTransaction[0];

				// Fetch previous wallet state from transactions file
				if (File.Exists(TransactionsFilePath))
				{
					try
					{
						IEnumerable<SmartTransaction> transactions = null;
						string jsonString = File.ReadAllText(TransactionsFilePath, Encoding.UTF8);
						transactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.OrderByBlockchain();

						confirmedTransactions = transactions?.Where(x => x.Confirmed)?.ToArray() ?? new SmartTransaction[0];

						unconfirmedTransactions = transactions?.Where(x => !x.Confirmed)?.ToArray() ?? new SmartTransaction[0];
					}
					catch (Exception ex)
					{
						Logger.LogWarning(ex);
						Logger.LogWarning($"Transaction cache got corrupted. Deleting {TransactionsFilePath}.");
						File.Delete(TransactionsFilePath);
					}
				}

				await LoadWalletStateAsync(confirmedTransactions, cancel);

				// Load in dummy mempool
				try
				{
					if (unconfirmedTransactions != null && unconfirmedTransactions.Any())
					{
						await LoadDummyMempoolAsync(unconfirmedTransactions);
					}
				}
				finally
				{
					UnconfirmedTransactionsInitialized = true;
				}
			}
			Coins.CollectionChanged += Coins_CollectionChanged;
			RefreshCoinHistories();
		}

		private async Task LoadWalletStateAsync(SmartTransaction[] confirmedTransactions, CancellationToken cancel)
		{
			KeyManager.AssertNetworkOrClearBlockState(Network);
			Height bestKeyManagerHeight = KeyManager.GetBestHeight();

			foreach (BlockState blockState in KeyManager.GetTransactionIndex())
			{
				var relevantTransactions = confirmedTransactions.Where(x => x.BlockHash == blockState.BlockHash).ToArray();
				var block = await FetchBlockAsync(blockState.BlockHash, cancel);
				await ProcessBlockAsync(blockState.BlockHeight, block, blockState.TransactionIndices, relevantTransactions);
			}

			// Go through the filters and queue to download the matches.
			await BitcoinStore.IndexStore.ForeachFiltersAsync(async (filterModel) =>
			{
				if (filterModel.Filter != null) // Filter can be null if there is no bech32 tx.
				{
					await ProcessFilterModelAsync(filterModel, cancel);
				}
			}, new Height(bestKeyManagerHeight.Value + 1));
		}

		private async Task LoadDummyMempoolAsync(SmartTransaction[] unconfirmedTransactions)
		{
			try
			{
				using (await TransactionProcessingLock.LockAsync())
				using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
				{
					var compactness = 10;

					var mempoolHashes = await client.GetMempoolHashesAsync(compactness);

					var count = 0;
					foreach (var tx in unconfirmedTransactions)
					{
						if (mempoolHashes.Contains(tx.GetHash().ToString().Substring(0, compactness)))
						{
							tx.SetHeight(Height.Mempool);
							await ProcessTransactionAsync(tx);
							Mempool.TransactionHashes.TryAdd(tx.GetHash());

							Logger.LogInfo($"Transaction was successfully tested against the backend's mempool hashes: {tx.GetHash()}.");
							count++;
						}
					}

					if (count != unconfirmedTransactions.Length)
					{
						SerializeTransactionCache();
					}
				}
			}
			catch (Exception ex)
			{
				// When there's a connection failure do not clean the transactions, add them to processing.
				foreach (var tx in unconfirmedTransactions)
				{
					tx.SetHeight(Height.Mempool);
					await ProcessTransactionAsync(tx);
					Mempool.TransactionHashes.TryAdd(tx.GetHash());
				}

				Logger.LogWarning(ex);
			}
		}

		private async Task ProcessFilterModelAsync(FilterModel filterModel, CancellationToken cancel)
		{
			if (ProcessedBlocks.ContainsKey(filterModel.BlockHash))
			{
				return;
			}

			var matchFound = filterModel.Filter.MatchAny(KeyManager.GetPubKeyScriptBytes(), filterModel.FilterKey);
			if (!matchFound)
			{
				return;
			}

			Block currentBlock = await FetchBlockAsync(filterModel.BlockHash, cancel); // Wait until not downloaded.

			if (await ProcessBlockAsync(filterModel.BlockHeight, currentBlock))
			{
				SerializeTransactionCache();
			}
		}

		public HdPubKey GetReceiveKey(SmartLabel label, IEnumerable<HdPubKey> dontTouch = null)
		{
			// Make sure there's always 21 clean keys generated and indexed.
			KeyManager.AssertCleanKeysIndexed(isInternal: false);

			IEnumerable<HdPubKey> keys = KeyManager.GetKeys(KeyState.Clean, isInternal: false);
			if (dontTouch != null)
			{
				keys = keys.Except(dontTouch);
				if (!keys.Any())
				{
					throw new InvalidOperationException($"{nameof(dontTouch)} covers all the possible keys.");
				}
			}

			var foundLabelless = keys.FirstOrDefault(x => x.Label.IsEmpty); // Return the first labelless.
			HdPubKey ret = foundLabelless ?? keys.RandomElement(); // Return the first, because that's the oldest.

			ret.SetLabel(label, KeyManager);

			return ret;
		}

		public List<SmartCoin> GetClusters(SmartCoin coin, List<SmartCoin> current, ILookup<Script, SmartCoin> lookupScriptPubKey, ILookup<uint256, SmartCoin> lookupSpenderTransactionId, ILookup<uint256, SmartCoin> lookupTransactionId)
		{
			Guard.NotNull(nameof(coin), coin);
			if (current.Contains(coin))
			{
				return current;
			}

			var clusters = current.Concat(new List<SmartCoin> { coin }).ToList(); // The coin is the first elem in its cluster.

			// If the script is the same then we have a match, no matter of the anonymity set.
			foreach (var c in lookupScriptPubKey[coin.ScriptPubKey])
			{
				if (!clusters.Contains(c))
				{
					var h = GetClusters(c, clusters, lookupScriptPubKey, lookupSpenderTransactionId, lookupTransactionId);
					foreach (var hr in h)
					{
						if (!clusters.Contains(hr))
						{
							clusters.Add(hr);
						}
					}
				}
			}

			// If it spends someone and has not been sufficiently anonymized.
			if (coin.AnonymitySet < ServiceConfiguration.PrivacyLevelStrong)
			{
				var c = lookupSpenderTransactionId[coin.TransactionId].FirstOrDefault(x => !clusters.Contains(x));
				if (c != default)
				{
					var h = GetClusters(c, clusters, lookupScriptPubKey, lookupSpenderTransactionId, lookupTransactionId);
					foreach (var hr in h)
					{
						if (!clusters.Contains(hr))
						{
							clusters.Add(hr);
						}
					}
				}
			}

			// If it's being spent by someone and that someone has not been sufficiently anonymized.
			if (!coin.Unspent)
			{
				var c = lookupTransactionId[coin.SpenderTransactionId].FirstOrDefault(x => !clusters.Contains(x));
				if (c != default)
				{
					if (c.AnonymitySet < ServiceConfiguration.PrivacyLevelStrong)
					{
						if (c != default)
						{
							var h = GetClusters(c, clusters, lookupScriptPubKey, lookupSpenderTransactionId, lookupTransactionId);
							foreach (var hr in h)
							{
								if (!clusters.Contains(hr))
								{
									clusters.Add(hr);
								}
							}
						}
					}
				}
			}

			return clusters;
		}

		private async Task<bool> ProcessBlockAsync(Height height, Block block, IEnumerable<int> filterByTxIndexes = null, IEnumerable<SmartTransaction> skeletonBlock = null)
		{
			var ret = false;
			using (await TransactionProcessingLock.LockAsync())
			{
				if (filterByTxIndexes is null)
				{
					var relevantIndicies = new List<int>();
					for (int i = 0; i < block.Transactions.Count; i++)
					{
						Transaction tx = block.Transactions[i];
						if (await ProcessTransactionAsync(new SmartTransaction(tx, height, block.GetHash(), i)))
						{
							relevantIndicies.Add(i);
							ret = true;
						}
					}

					if (relevantIndicies.Any())
					{
						var blockState = new BlockState(block.GetHash(), height, relevantIndicies);
						KeyManager.AddBlockState(blockState, setItsHeightToBest: true); // Set the height here (so less toFile and lock.)
					}
					else
					{
						KeyManager.SetBestHeight(height);
					}
				}
				else
				{
					foreach (var i in filterByTxIndexes.OrderBy(x => x))
					{
						var tx = skeletonBlock?.FirstOrDefault(x => x.BlockIndex == i) ?? new SmartTransaction(block.Transactions[i], height, block.GetHash(), i);
						if (await ProcessTransactionAsync(tx))
						{
							ret = true;
						}
					}
				}
			}

			ProcessedBlocks.TryAdd(block.GetHash(), (height, block.Header.BlockTime));

			NewBlockProcessed?.Invoke(this, block);

			return ret;
		}

		private async Task<bool> ProcessTransactionAsync(SmartTransaction tx)
		{
			return await Task.FromResult(TransactionProcessor.Process(tx));
		}

		private Node _localBitcoinCoreNode = null;

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

		/// <param name="hash">Block hash of the desired block, represented as a 256 bit integer.</param>
		/// <exception cref="OperationCanceledException"></exception>
		private async Task<Block> TryGetBlockFromFileAsync(uint256 hash, CancellationToken cancel)
		{
			// Try get the block
			Block block = null;
			using (await BlockFolderLock.LockAsync())
			{
				var encoder = new HexEncoder();
				var filePath = Path.Combine(BlocksFolderPath, hash.ToString());
				if (File.Exists(filePath))
				{
					try
					{
						var blockBytes = await File.ReadAllBytesAsync(filePath, cancel);
						block = Block.Load(blockBytes, Synchronizer.Network);
					}
					catch (Exception)
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
				await BlockDownloadLock.LockAsync();
				DownloadingBlock = true;

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
							await Task.Delay(100);
						}

						// Select a random node we are connected to.
						Node node = Nodes.ConnectedNodes.RandomElement();
						if (node == default(Node) && !node.IsConnected && Synchronizer.Network == Network.RegTest)
						{
							await Task.Delay(100);
							continue;
						}

						// Download block from selected node.
						try
						{
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(RuntimeParams.Instance.NetworkNodeTimeout))) // 1/2 ADSL	512 kbit/s	00:00:32
							{
								block = await node.DownloadBlockAsync(hash, cts.Token);
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
						catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
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
				using (await BlockFolderLock.LockAsync())
				{
					var path = Path.Combine(BlocksFolderPath, hash.ToString());
					await File.WriteAllBytesAsync(path, block.ToBytes());
				}
			}
			finally
			{
				DownloadingBlock = false;
				BlockDownloadLock.ReleaseLock();
			}

			return block;
		}

		private async Task<Block> TryDownloadBlockFromLocalNodeAsync(uint256 hash, CancellationToken cancel)
		{
			try
			{
				if (LocalBitcoinCoreNode is null || (!LocalBitcoinCoreNode.IsConnected && Network != Network.RegTest)) // If RegTest then we're already connected do not try again.
				{
					DisconnectDisposeNullLocalBitcoinCoreNode();
					using (var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
					{
						handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
						var nodeConnectionParameters = new NodeConnectionParameters()
						{
							ConnectCancellation = handshakeTimeout.Token,
							IsRelay = false,
							UserAgent = $"/Wasabi:{Constants.ClientVersion.ToVersionString()}/"
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
				else
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

		/// <remarks>
		/// Use it at reorgs.
		/// </remarks>
		public async Task DeleteBlockAsync(uint256 hash)
		{
			try
			{
				using (await BlockFolderLock.LockAsync())
				{
					var filePaths = Directory.EnumerateFiles(BlocksFolderPath);
					var fileNames = filePaths.Select(Path.GetFileName);
					var hashes = fileNames.Select(x => new uint256(x));

					if (hashes.Contains(hash))
					{
						File.Delete(Path.Combine(BlocksFolderPath, hash.ToString()));
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
				return Directory.EnumerateFiles(BlocksFolderPath).Count();
			}
		}

		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		/// <param name="allowedInputs">Only these inputs allowed to be used to build the transaction. The wallet must know the corresponding private keys.</param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public BuildTransactionResult BuildTransaction(string password,
														PaymentIntent payments,
														FeeStrategy feeStrategy,
														bool allowUnconfirmed = false,
														IEnumerable<TxoRef> allowedInputs = null)
		{
			password = password ?? ""; // Correction.
			payments = Guard.NotNull(nameof(payments), payments);

			long totalAmount = payments.TotalAmount.Satoshi;
			if (totalAmount < 0 || totalAmount > Constants.MaximumNumberOfSatoshis)
			{
				throw new ArgumentOutOfRangeException($"{nameof(payments)}.{nameof(payments.TotalAmount)} sum cannot be smaller than 0 or greater than {Constants.MaximumNumberOfSatoshis}.");
			}

			// Get allowed coins to spend.
			List<SmartCoin> allowedSmartCoinInputs; // Inputs that can be used to build the transaction.
			if (allowedInputs != null) // If allowedInputs are specified then select the coins from them.
			{
				if (!allowedInputs.Any())
				{
					throw new ArgumentException($"{nameof(allowedInputs)} is not null, but empty.");
				}

				allowedSmartCoinInputs = allowUnconfirmed
					? Coins.Where(x => !x.Unavailable && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList()
					: Coins.Where(x => !x.Unavailable && x.Confirmed && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();

				// Add those that have the same script, because common ownership is already exposed.
				// But only if the user didn't click the "max" button. In this case he'd send more money than what he'd think.
				if (payments.ChangeStrategy != ChangeStrategy.AllRemainingCustom)
				{
					var allScripts = allowedSmartCoinInputs.Select(x => x.ScriptPubKey).ToHashSet();
					foreach (var coin in Coins.Where(x => !x.Unavailable && !allowedSmartCoinInputs.Any(y => x.TransactionId == y.TransactionId && x.Index == y.Index)))
					{
						if (!(allowUnconfirmed || coin.Confirmed))
						{
							continue;
						}

						if (allScripts.Contains(coin.ScriptPubKey))
						{
							allowedSmartCoinInputs.Add(coin);
						}
					}
				}
			}
			else
			{
				allowedSmartCoinInputs = allowUnconfirmed ? Coins.Where(x => !x.Unavailable).ToList() : Coins.Where(x => !x.Unavailable && x.Confirmed).ToList();
			}

			// Get and calculate fee
			Logger.LogInfo("Calculating dynamic transaction fee...");

			FeeRate feeRate;
			if (feeStrategy.Type == FeeStrategyType.Target)
			{
				feeRate = Synchronizer.GetFeeRate(feeStrategy.Target);
			}
			else if (feeStrategy.Type == FeeStrategyType.Rate)
			{
				feeRate = feeStrategy.Rate;
			}
			else
			{
				throw new NotSupportedException(feeStrategy.Type.ToString());
			}

			var smartCoinsByOutpoint = allowedSmartCoinInputs.ToDictionary(s => s.GetOutPoint());
			TransactionBuilder builder = Network.CreateTransactionBuilder();
			builder.SetCoinSelector(new SmartCoinSelector(smartCoinsByOutpoint));
			builder.AddCoins(allowedSmartCoinInputs.Select(c => c.GetCoin()));

			foreach (var request in payments.Requests.Where(x => x.Amount.Type == MoneyRequestType.Value))
			{
				var amountRequest = request.Amount;

				builder.Send(request.Destination, amountRequest.Amount);
				if (amountRequest.SubtractFee)
				{
					builder.SubtractFees();
				}
			}

			HdPubKey changeHdPubKey = null;

			if (payments.TryGetCustomRequest(out DestinationRequest custChange))
			{
				var changeScript = custChange.Destination.ScriptPubKey;
				changeHdPubKey = KeyManager.GetKeyForScriptPubKey(changeScript);

				var changeStrategy = payments.ChangeStrategy;
				if (changeStrategy == ChangeStrategy.Custom)
				{
					builder.SetChange(changeScript);
				}
				else if (changeStrategy == ChangeStrategy.AllRemainingCustom)
				{
					builder.SendAllRemaining(changeScript);
				}
				else
				{
					throw new NotSupportedException(payments.ChangeStrategy.ToString());
				}
			}
			else
			{
				KeyManager.AssertCleanKeysIndexed(isInternal: true);
				KeyManager.AssertLockedInternalKeysIndexed(14);
				changeHdPubKey = KeyManager.GetKeys(KeyState.Clean, true).RandomElement();

				builder.SetChange(changeHdPubKey.P2wpkhScript);
			}

			builder.SendEstimatedFees(feeRate);

			var psbt = builder.BuildPSBT(false);

			var spentCoins = psbt.Inputs.Select(txin => smartCoinsByOutpoint[txin.PrevOut]).ToArray();

			var realToSend = payments.Requests
				.Select(t =>
					(label: t.Label,
					destination: t.Destination,
					amount: psbt.Outputs.FirstOrDefault(o => o.ScriptPubKey == t.Destination.ScriptPubKey)?.Value))
				.Where(i => i.amount != null);

			if (!psbt.TryGetFee(out var fee))
			{
				throw new InvalidOperationException("Impossible to get the fees of the PSBT, this should never happen.");
			}
			Logger.LogInfo($"Fee: {fee.Satoshi} Satoshi.");

			var vSize = builder.EstimateSize(psbt.GetOriginalTransaction(), true);
			Logger.LogInfo($"Estimated tx size: {vSize} vbytes.");

			// Do some checks
			Money totalSendAmountNoFee = realToSend.Sum(x => x.amount);
			if (totalSendAmountNoFee == Money.Zero)
			{
				throw new InvalidOperationException("The amount after subtracting the fee is too small to be sent.");
			}
			Money totalSendAmount = totalSendAmountNoFee + fee;

			Money totalOutgoingAmountNoFee;
			if (changeHdPubKey is null)
			{
				totalOutgoingAmountNoFee = totalSendAmountNoFee;
			}
			else
			{
				totalOutgoingAmountNoFee = realToSend.Where(x => !changeHdPubKey.ContainsScript(x.destination.ScriptPubKey)).Sum(x => x.amount);
			}
			decimal totalOutgoingAmountNoFeeDecimal = totalOutgoingAmountNoFee.ToDecimal(MoneyUnit.BTC);
			// Cannot divide by zero, so use the closest number we have to zero.
			decimal totalOutgoingAmountNoFeeDecimalDivisor = totalOutgoingAmountNoFeeDecimal == 0 ? decimal.MinValue : totalOutgoingAmountNoFeeDecimal;
			decimal feePc = (100 * fee.ToDecimal(MoneyUnit.BTC)) / totalOutgoingAmountNoFeeDecimalDivisor;

			if (feePc > 1)
			{
				Logger.LogInfo($"The transaction fee is {totalOutgoingAmountNoFee:0.#}% of your transaction amount.{Environment.NewLine}"
					+ $"Sending:\t {totalSendAmount.ToString(fplus: false, trimExcessZero: true)} BTC.{Environment.NewLine}"
					+ $"Fee:\t\t {fee.Satoshi} Satoshi.");
			}
			if (feePc > 100)
			{
				throw new InvalidOperationException($"The transaction fee is more than twice the transaction amount: {feePc:0.#}%.");
			}

			if (spentCoins.Any(u => !u.Confirmed))
			{
				Logger.LogInfo("Unconfirmed transaction is spent.");
			}

			// Build the transaction
			Logger.LogInfo("Signing transaction...");
			// It must be watch only, too, because if we have the key and also hardware wallet, we do not care we can sign.

			Transaction tx = null;
			if (KeyManager.IsWatchOnly)
			{
				tx = psbt.GetGlobalTransaction();
			}
			else
			{
				IEnumerable<ExtKey> signingKeys = KeyManager.GetSecrets(password, spentCoins.Select(x => x.ScriptPubKey).ToArray());
				builder = builder.AddKeys(signingKeys.ToArray());
				builder.SignPSBT(psbt);
				psbt.Finalize();
				tx = psbt.ExtractTransaction();

				var checkResults = builder.Check(tx).ToList();
				if (!psbt.TryGetEstimatedFeeRate(out FeeRate actualFeeRate))
				{
					throw new InvalidOperationException("Impossible to get the fee rate of the PSBT, this should never happen.");
				}

				// Manually check the feerate, because some inaccuracy is possible.
				var sb1 = feeRate.SatoshiPerByte;
				var sb2 = actualFeeRate.SatoshiPerByte;
				if (Math.Abs(sb1 - sb2) > 2) // 2s/b inaccuracy ok.
				{
					// So it'll generate a transactionpolicy error thrown below.
					checkResults.Add(new NotEnoughFundsPolicyError("Fees different than expected"));
				}
				if (checkResults.Count > 0)
				{
					throw new InvalidTxException(tx, checkResults);
				}
			}

			if (KeyManager.MasterFingerprint is HDFingerprint fp)
			{
				foreach (var coin in spentCoins)
				{
					var rootKeyPath = new RootedKeyPath(fp, coin.HdPubKey.FullKeyPath);
					psbt.AddKeyPath(coin.HdPubKey.PubKey, rootKeyPath, coin.ScriptPubKey);
				}
			}

			var label = SmartLabel.Merge(payments.Requests.Select(x => x.Label));
			var outerWalletOutputs = new List<SmartCoin>();
			var innerWalletOutputs = new List<SmartCoin>();
			for (var i = 0U; i < tx.Outputs.Count; i++)
			{
				TxOut output = tx.Outputs[i];
				var anonset = (tx.GetAnonymitySet(i) + spentCoins.Min(x => x.AnonymitySet)) - 1; // Minus 1, because count own only once.
				var foundKey = KeyManager.GetKeyForScriptPubKey(output.ScriptPubKey);
				var coin = new SmartCoin(tx.GetHash(), i, output.ScriptPubKey, output.Value, tx.Inputs.ToTxoRefs().ToArray(), Height.Unknown, tx.RBF, anonset, isLikelyCoinJoinOutput: false, pubKey: foundKey);
				label = SmartLabel.Merge(label, coin.Label); // foundKey's label is already added to the coinlabel.

				if (foundKey is null)
				{
					outerWalletOutputs.Add(coin);
				}
				else
				{
					innerWalletOutputs.Add(coin);
				}
			}

			foreach (var coin in outerWalletOutputs.Concat(innerWalletOutputs))
			{
				var foundPaymentRequest = payments.Requests.FirstOrDefault(x => x.Destination.ScriptPubKey == coin.ScriptPubKey);

				// If change then we concatenate all the labels.
				if (foundPaymentRequest is null) // Then it's autochange.
				{
					coin.Label = label;
				}
				else
				{
					coin.Label = SmartLabel.Merge(coin.Label, foundPaymentRequest.Label);
				}

				var foundKey = KeyManager.GetKeyForScriptPubKey(coin.ScriptPubKey);
				foundKey?.SetLabel(coin.Label); // The foundkeylabel has already been added previously, so no need to concatenate.
			}

			Logger.LogInfo($"Transaction is successfully built: {tx.GetHash()}.");
			var sign = !KeyManager.IsWatchOnly;
			var spendsUnconfirmed = spentCoins.Any(c => !c.Confirmed);
			return new BuildTransactionResult(new SmartTransaction(tx, Height.Unknown), psbt, spendsUnconfirmed, sign, fee, feePc, outerWalletOutputs, innerWalletOutputs, spentCoins);
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

		private static long SendCount = 0;

		public async Task SendTransactionAsync(SmartTransaction transaction)
		{
			try
			{
				Interlocked.Increment(ref SendCount);
				// Broadcast to a random node.
				// Wait until it arrives to at least two other nodes.
				// If something's wrong, fall back broadcasting with backend.

				if (Network == Network.RegTest)
				{
					throw new InvalidOperationException($"Transaction broadcasting to nodes does not work in {Network.RegTest}.");
				}

				while (true)
				{
					// As long as we are connected to at least 4 nodes, we can always try again.
					// 3 should be enough, but make it 5 so 2 nodes could disconnect the meantime.
					if (Nodes.ConnectedNodes.Count < 5)
					{
						throw new InvalidOperationException("We are not connected to enough nodes.");
					}

					Node node = Nodes.ConnectedNodes.RandomElement();
					if (node == default(Node))
					{
						await Task.Delay(100);
						continue;
					}

					if (!node.IsConnected)
					{
						await Task.Delay(100);
						continue;
					}

					Logger.LogInfo($"Trying to broadcast transaction with random node ({node.RemoteSocketAddress}):{transaction.GetHash()}.");
					var addedToBroadcastStore = Mempool.TryAddToBroadcastStore(transaction.Transaction, node.RemoteSocketEndpoint.ToString()); // So we'll reply to INV with this transaction.
					if (!addedToBroadcastStore)
					{
						Logger.LogWarning($"Transaction {transaction.GetHash()} was already present in the broadcast store.");
					}
					var invPayload = new InvPayload(transaction.Transaction);
					// Give 7 seconds to send the inv payload.
					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7)))
					{
						await node.SendMessageAsync(invPayload).WithCancellation(cts.Token); // ToDo: It's dangerous way to cancel. Implement proper cancellation to NBitcoin!
					}

					if (Mempool.TryGetFromBroadcastStore(transaction.GetHash(), out TransactionBroadcastEntry entry))
					{
						// Give 7 seconds for serving.
						var timeout = 0;
						while (!entry.IsBroadcasted())
						{
							if (timeout > 7)
							{
								throw new TimeoutException("Did not serve the transaction.");
							}
							await Task.Delay(1_000);
							timeout++;
						}
						node.DisconnectAsync("Thank you!");
						Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}. Successfully broadcasted transaction: {transaction.GetHash()}.");

						// Give 21 seconds for propagation.
						timeout = 0;
						while (entry.GetPropagationConfirmations() < 2)
						{
							if (timeout > 21)
							{
								throw new TimeoutException("Did not serve the transaction.");
							}
							await Task.Delay(1_000);
							timeout++;
						}
						Logger.LogInfo($"Transaction is successfully propagated: {transaction.GetHash()}.");
					}
					else
					{
						Logger.LogWarning($"Expected transaction {transaction.GetHash()} was not found in the broadcast store.");
					}
					break;
				}
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Random node could not broadcast transaction. Broadcasting with backend... Reason: {ex.Message}.");
				Logger.LogDebug(ex);

				using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
				{
					try
					{
						await client.BroadcastAsync(transaction);
					}
					catch (HttpRequestException ex2) when (
						ex2.Message.Contains("bad-txns-inputs-missingorspent", StringComparison.InvariantCultureIgnoreCase)
						|| ex2.Message.Contains("missing-inputs", StringComparison.InvariantCultureIgnoreCase)
						|| ex2.Message.Contains("txn-mempool-conflict", StringComparison.InvariantCultureIgnoreCase))
					{
						if (transaction.Transaction.Inputs.Count == 1) // If we tried to only spend one coin, then we can mark it as spent. If there were more coins, then we do not know.
						{
							OutPoint input = transaction.Transaction.Inputs.First().PrevOut;
							SmartCoin coin = Coins.FirstOrDefault(x => x.TransactionId == input.Hash && x.Index == input.N);
							if (coin != default)
							{
								coin.SpentAccordingToBackend = true;
							}
						}
					}
				}

				using (await TransactionProcessingLock.LockAsync())
				{
					if (await ProcessTransactionAsync(new SmartTransaction(transaction.Transaction, Height.Mempool)))
					{
						SerializeTransactionCache();
					}

					Mempool.TransactionHashes.TryAdd(transaction.GetHash());
				}

				Logger.LogInfo($"Transaction is successfully broadcasted to backend: {transaction.GetHash()}.");
			}
			finally
			{
				Mempool.TryRemoveFromBroadcastStore(transaction.GetHash(), out _); // Remove it just to be sure. Probably has been removed previously.
				Interlocked.Decrement(ref SendCount);
			}
		}

		public ISet<string> GetLabels() => Coins
			.SelectMany(x => x.Label.Labels)
			.Concat(KeyManager
				.GetKeys()
				.SelectMany(x => x.Label.Labels))
			.ToHashSet();

		private int _refreshCoinHistoriesRerunRequested = 0;
		private int _refreshCoinHistoriesRunning = 0;

		public void RefreshCoinHistories()
		{
			// If already running, then make sure another run is requested, else do the work.
			if (Interlocked.CompareExchange(ref _refreshCoinHistoriesRunning, 1, 0) == 1)
			{
				Interlocked.Exchange(ref _refreshCoinHistoriesRerunRequested, 1);
				return;
			}

			try
			{
				var unspentCoins = Coins.Where(c => c.Unspent); //refreshing unspent coins clusters only
				if (unspentCoins.Any())
				{
					ILookup<Script, SmartCoin> lookupScriptPubKey = Coins.ToLookup(c => c.ScriptPubKey, c => c);
					ILookup<uint256, SmartCoin> lookupSpenderTransactionId = Coins.ToLookup(c => c.SpenderTransactionId, c => c);
					ILookup<uint256, SmartCoin> lookupTransactionId = Coins.ToLookup(c => c.TransactionId, c => c);

					const int simultaneousThread = 2; //threads allowed to run simultaneously in threadpool

					Parallel.ForEach(unspentCoins, new ParallelOptions { MaxDegreeOfParallelism = simultaneousThread }, coin =>
					{
						var result = string.Join(
							", ",
							GetClusters(coin, new List<SmartCoin>(), lookupScriptPubKey, lookupSpenderTransactionId, lookupTransactionId)
							.SelectMany(x => x.Label.Labels)
							.Distinct(StringComparer.OrdinalIgnoreCase));
						coin.SetClusters(result);
					});
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Refreshing coin clusters failed: {ex}.");
			}
			finally
			{
				// It's not running anymore, but someone may requested another run.
				Interlocked.Exchange(ref _refreshCoinHistoriesRunning, 0);

				// Clear the rerun request, too and if it was requested, then rerun.
				if (Interlocked.Exchange(ref _refreshCoinHistoriesRerunRequested, 0) == 1)
				{
					RefreshCoinHistories();
				}
			}
		}

		public bool UnconfirmedTransactionsInitialized { get; private set; } = false;

		private void SerializeTransactionCache()
		{
			if (!UnconfirmedTransactionsInitialized) // If unconfirmed ones are not yet initialized, then do not serialize because unconfirmed are going to be lost.
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(TransactionsFilePath);
			string jsonString = JsonConvert.SerializeObject(TransactionCache.OrderByBlockchain(), Formatting.Indented);
			File.WriteAllText(TransactionsFilePath,
				jsonString,
				Encoding.UTF8);
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

		public async Task StopAsync()
		{
			while (Interlocked.Read(ref SendCount) != 0) // Make sure to wait for send to finish.
			{
				await Task.Delay(50);
			}

			BitcoinStore.IndexStore.NewFilter -= IndexDownloader_NewFilterAsync;
			BitcoinStore.IndexStore.Reorged -= IndexDownloader_ReorgedAsync;
			Mempool.TransactionReceived -= Mempool_TransactionReceivedAsync;
			Coins.CollectionChanged -= Coins_CollectionChanged;
			TransactionProcessor.CoinSpent -= TransactionProcessor_CoinSpent;
			TransactionProcessor.CoinReceived -= TransactionProcessor_CoinReceivedAsync;

			DisconnectDisposeNullLocalBitcoinCoreNode();
		}

		public SmartTransaction TryGetTxFromCache(uint256 txId)
		{
			return TransactionCache.FirstOrDefault(x => x.GetHash() == txId);
		}
	}
}
