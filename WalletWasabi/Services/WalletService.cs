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
using WalletWasabi.Mempool;
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
		public NodesGroup Nodes { get; }
		public string BlocksFolderPath { get; }

		private AsyncLock HandleFiltersLock { get; }

		private AsyncLock BlockFolderLock { get; }

		private int NodeTimeouts { get; set; }

		public ServiceConfiguration ServiceConfiguration { get; }

		public ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)> ProcessedBlocks { get; }

		public ICoinsView Coins { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<Block> NewBlockProcessed;

		public Network Network => Synchronizer.Network;

		public TransactionProcessor TransactionProcessor { get; }

		public WalletService(
			BitcoinStore bitcoinStore,
			KeyManager keyManager,
			WasabiSynchronizer syncer,
			CcjClient chaumianClient,
			NodesGroup nodes,
			string workFolderDir,
			ServiceConfiguration serviceConfiguration)
		{
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			Synchronizer = Guard.NotNull(nameof(syncer), syncer);
			ChaumianClient = Guard.NotNull(nameof(chaumianClient), chaumianClient);
			ServiceConfiguration = Guard.NotNull(nameof(serviceConfiguration), serviceConfiguration);

			ProcessedBlocks = new ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)>();
			HandleFiltersLock = new AsyncLock();

			BlocksFolderPath = Path.Combine(workFolderDir, "Blocks", Network.ToString());
			RuntimeParams.SetDataDir(workFolderDir);

			BlockFolderLock = new AsyncLock();

			KeyManager.AssertCleanKeysIndexed();
			KeyManager.AssertLockedInternalKeysIndexed(14);

			TransactionProcessor = new TransactionProcessor(BitcoinStore.TransactionStore, KeyManager, ServiceConfiguration.DustThreshold, ServiceConfiguration.PrivacyLevelStrong);
			Coins = TransactionProcessor.Coins;
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

			var walletName = "UnnamedWallet";
			if (!string.IsNullOrWhiteSpace(KeyManager.FilePath))
			{
				walletName = Path.GetFileNameWithoutExtension(KeyManager.FilePath);
			}

			BitcoinStore.IndexStore.NewFilter += IndexDownloader_NewFilterAsync;
			BitcoinStore.IndexStore.Reorged += IndexDownloader_ReorgedAsync;
			BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;
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

		private static object TransactionProcessingLock { get; } = new object();

		private void Mempool_TransactionReceived(object sender, SmartTransaction tx)
		{
			try
			{
				lock (TransactionProcessingLock)
				{
					TransactionProcessor.Process(tx);
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
						TransactionProcessor.UndoBlock(blockState.BlockHeight);
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

				await BitcoinStore.MempoolService?.TryPerformMempoolCleanupAsync(Synchronizer?.WasabiClient?.TorClient?.DestinationUriAction, Synchronizer?.WasabiClient?.TorClient?.TorSocks5EndPoint);
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

			while (!BitcoinStore.IsInitialized)
			{
				await Task.Delay(100).ConfigureAwait(false);

				cancel.ThrowIfCancellationRequested();
			}

			await RuntimeParams.LoadAsync();

			using (await HandleFiltersLock.LockAsync())
			{
				await LoadWalletStateAsync(cancel);
				await LoadDummyMempoolAsync();
			}
		}

		private async Task LoadWalletStateAsync(CancellationToken cancel)
		{
			KeyManager.AssertNetworkOrClearBlockState(Network);
			Height bestKeyManagerHeight = KeyManager.GetBestHeight();

			foreach (BlockState blockState in KeyManager.GetTransactionIndex())
			{
				var relevantTransactions = BitcoinStore.TransactionStore.ConfirmedStore.GetTransactions().Where(x => x.BlockHash == blockState.BlockHash).ToArray();
				var block = await FetchBlockAsync(blockState.BlockHash, cancel);
				ProcessBlock(blockState.BlockHeight, block, blockState.TransactionIndices, relevantTransactions);
			}

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

			try
			{
				using var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint);
				var compactness = 10;

				var mempoolHashes = await client.GetMempoolHashesAsync(compactness);

				lock (TransactionProcessingLock)
				{
					foreach (var tx in BitcoinStore.TransactionStore.MempoolStore.GetTransactions())
					{
						uint256 hash = tx.GetHash();
						if (mempoolHashes.Contains(hash.ToString().Substring(0, compactness)))
						{
							TransactionProcessor.Process(tx);

							Logger.LogInfo($"Transaction was successfully tested against the backend's mempool hashes: {hash}.");
						}
						else
						{
							BitcoinStore.TransactionStore.MempoolStore.TryRemove(tx.GetHash(), out _);
						}
					}
				}
			}
			catch (Exception ex)
			{
				// When there's a connection failure do not clean the transactions, add them to processing.
				foreach (var tx in BitcoinStore.TransactionStore.MempoolStore.GetTransactions())
				{
					TransactionProcessor.Process(tx);
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
			ProcessBlock(filterModel.BlockHeight, currentBlock);
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

		private void ProcessBlock(Height height, Block block, IEnumerable<int> filterByTxIndexes = null, IEnumerable<SmartTransaction> skeletonBlock = null)
		{
			lock (TransactionProcessingLock)
			{
				if (filterByTxIndexes is null)
				{
					var relevantIndices = new List<int>();
					for (int i = 0; i < block.Transactions.Count; i++)
					{
						Transaction tx = block.Transactions[i];
						if (TransactionProcessor.Process(new SmartTransaction(tx, height, block.GetHash(), i, firstSeen: block.Header.BlockTime)))
						{
							relevantIndices.Add(i);
						}
					}

					if (relevantIndices.Any())
					{
						var blockState = new BlockState(block.GetHash(), height, relevantIndices);
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
						TransactionProcessor.Process(tx);
					}
				}
			}

			ProcessedBlocks.TryAdd(block.GetHash(), (height, block.Header.BlockTime));

			NewBlockProcessed?.Invoke(this, block);
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
					using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
					handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
					var nodeConnectionParameters = new NodeConnectionParameters()
					{
						ConnectCancellation = handshakeTimeout.Token,
						IsRelay = false,
						UserAgent = $"/Wasabi:{Constants.ClientVersion.ToString()}/"
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
			var builder = new TransactionFactory(Network, KeyManager, Coins, password, allowUnconfirmed);
			return builder.BuildTransaction(
				payments,
				() =>
				{
					if (feeStrategy.Type == FeeStrategyType.Target)
					{
						return Synchronizer.GetFeeRate(feeStrategy.Target);
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
				allowedInputs);
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

		private async Task BroadcastTransactionToNetworkNodeAsync(SmartTransaction transaction, Node node)
		{
			Logger.LogInfo($"Trying to broadcast transaction with random node ({node.RemoteSocketAddress}):{transaction.GetHash()}.");
			if (!BitcoinStore.MempoolService.TryAddToBroadcastStore(transaction.Transaction, node.RemoteSocketEndpoint.ToString())) // So we'll reply to INV with this transaction.
			{
				Logger.LogWarning($"Transaction {transaction.GetHash()} was already present in the broadcast store.");
			}
			var invPayload = new InvPayload(transaction.Transaction);
			// Give 7 seconds to send the inv payload.
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7)))
			{
				await node.SendMessageAsync(invPayload).WithCancellation(cts.Token); // ToDo: It's dangerous way to cancel. Implement proper cancellation to NBitcoin!
			}

			if (BitcoinStore.MempoolService.TryGetFromBroadcastStore(transaction.GetHash(), out TransactionBroadcastEntry entry))
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
		}

		private async Task BroadcastTransactionToBackendAsync(SmartTransaction transaction)
		{
			using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				try
				{
					await client.BroadcastAsync(transaction);
				}
				catch (HttpRequestException ex2) when (ex2.Message.Contains("bad-txns-inputs-missingorspent", StringComparison.InvariantCultureIgnoreCase)
					|| ex2.Message.Contains("missing-inputs", StringComparison.InvariantCultureIgnoreCase)
					|| ex2.Message.Contains("txn-mempool-conflict", StringComparison.InvariantCultureIgnoreCase))
				{
					if (transaction.Transaction.Inputs.Count == 1) // If we tried to only spend one coin, then we can mark it as spent. If there were more coins, then we do not know.
					{
						OutPoint input = transaction.Transaction.Inputs.First().PrevOut;
						SmartCoin coin = Coins.GetByOutPoint(input);
						if (coin != default)
						{
							coin.SpentAccordingToBackend = true;
						}
					}
				}
			}

			lock (TransactionProcessingLock)
			{
				TransactionProcessor.Process(new SmartTransaction(transaction.Transaction, Height.Mempool));
			}

			Logger.LogInfo($"Transaction is successfully broadcasted to backend: {transaction.GetHash()}.");
		}

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

				Node node = Nodes.ConnectedNodes.RandomElement();
				while (node == default(Node) || !node.IsConnected || Nodes.ConnectedNodes.Count < 5)
				{
					// As long as we are connected to at least 4 nodes, we can always try again.
					// 3 should be enough, but make it 5 so 2 nodes could disconnect the meantime.
					if (Nodes.ConnectedNodes.Count < 5)
					{
						throw new InvalidOperationException("We are not connected to enough nodes.");
					}
					await Task.Delay(100);
					node = Nodes.ConnectedNodes.RandomElement();
				}
				await BroadcastTransactionToNetworkNodeAsync(transaction, node);
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Random node could not broadcast transaction. Broadcasting with backend... Reason: {ex.Message}.");
				Logger.LogDebug(ex);

				await BroadcastTransactionToBackendAsync(transaction);
			}
			finally
			{
				BitcoinStore.MempoolService.TryRemoveFromBroadcastStore(transaction.GetHash(), out _); // Remove it just to be sure. Probably has been removed previously.
				Interlocked.Decrement(ref SendCount);
			}
		}

		public ISet<string> GetLabels() => Coins
			.SelectMany(x => x.Label.Labels)
			.Concat(KeyManager
				.GetKeys()
				.SelectMany(x => x.Label.Labels))
			.ToHashSet();

		public void RefreshCoinHistories()
		{
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
			BitcoinStore.MempoolService.TransactionReceived -= Mempool_TransactionReceived;
			TransactionProcessor.CoinSpent -= TransactionProcessor_CoinSpent;
			TransactionProcessor.CoinReceived -= TransactionProcessor_CoinReceivedAsync;

			DisconnectDisposeNullLocalBitcoinCoreNode();
		}
	}
}
