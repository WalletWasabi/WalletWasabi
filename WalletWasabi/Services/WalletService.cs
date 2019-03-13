using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using NBitcoin;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using WalletWasabi.WebClients.Wasabi;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using NBitcoin.DataEncoders;
using System.Net.Http;
using System.Diagnostics;
using NBitcoin.BitcoinCore;
using System.Net.Sockets;

namespace WalletWasabi.Services
{
	public class WalletService : IDisposable
	{
		public KeyManager KeyManager { get; }
		public WasabiSynchronizer Synchronizer { get; }
		public CcjClient ChaumianClient { get; }
		public MemPoolService MemPool { get; }

		public NodesGroup Nodes { get; }
		public string BlocksFolderPath { get; }
		public string TransactionsFolderPath { get; }
		public string TransactionsFilePath { get; }

		private AsyncLock HandleFiltersLock { get; }
		private AsyncLock BlockDownloadLock { get; }
		private AsyncLock BlockFolderLock { get; }

		// These are static functions, so we will make sure when blocks are downloading with multiple wallet services, they don't conflict.
		private static int _concurrentBlockDownload = 0;

		/// <summary>
		/// int: number of blocks being downloaded in parallel, not the number of blocks left to download!
		/// </summary>
		public static event EventHandler<int> ConcurrentBlockDownloadNumberChanged;

		public ServiceConfiguration ServiceConfiguration { get; }

		public SortedDictionary<Height, uint256> WalletBlocks { get; }
		public ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)> ProcessedBlocks { get; }
		private AsyncLock WalletBlocksLock { get; }

		public ObservableConcurrentHashSet<SmartCoin> Coins { get; }

		public ConcurrentHashSet<SmartTransaction> TransactionCache { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<SmartCoin> CoinSpentOrSpenderConfirmed;

		public event EventHandler<Block> NewBlockProcessed;

		public Network Network => Synchronizer.Network;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public WalletService(
			KeyManager keyManager,
			WasabiSynchronizer syncer,
			CcjClient chaumianClient,
			MemPoolService memPool,
			NodesGroup nodes,
			string workFolderDir,
			ServiceConfiguration serviceConfiguration)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			Synchronizer = Guard.NotNull(nameof(syncer), syncer);
			ChaumianClient = Guard.NotNull(nameof(chaumianClient), chaumianClient);
			MemPool = Guard.NotNull(nameof(memPool), memPool);
			ServiceConfiguration = Guard.NotNull(nameof(serviceConfiguration), serviceConfiguration);

			WalletBlocks = new SortedDictionary<Height, uint256>();
			ProcessedBlocks = new ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)>();
			WalletBlocksLock = new AsyncLock();
			HandleFiltersLock = new AsyncLock();

			Coins = new ObservableConcurrentHashSet<SmartCoin>();
			TransactionCache = new ConcurrentHashSet<SmartTransaction>();

			BlocksFolderPath = Path.Combine(workFolderDir, "Blocks", Network.ToString());
			TransactionsFolderPath = Path.Combine(workFolderDir, "Transactions", Network.ToString());
			BlockFolderLock = new AsyncLock();
			BlockDownloadLock = new AsyncLock();

			KeyManager.AssertCleanKeysIndexed(21);
			KeyManager.AssertLockedInternalKeysIndexed(14);

			_running = 0;

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

			Synchronizer.NewFilter += IndexDownloader_NewFilterAsync;
			Synchronizer.Reorged += IndexDownloader_ReorgedAsync;
			MemPool.TransactionReceived += MemPool_TransactionReceived;
		}

		private void Coins_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			RefreshCoinsHistoriesAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}

		private static object TransactionProcessingLock { get; } = new object();

		private void MemPool_TransactionReceived(object sender, SmartTransaction tx)
		{
			try
			{
				lock (TransactionProcessingLock)
				{
					ProcessTransaction(tx);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<WalletService>(ex);
			}
		}

		private async void IndexDownloader_ReorgedAsync(object sender, FilterModel invalidFilter)
		{
			try
			{
				using (HandleFiltersLock.Lock())
				using (WalletBlocksLock.Lock())
				{
					uint256 invalidBlockHash = invalidFilter.BlockHash;
					KeyValuePair<Height, uint256> elem = WalletBlocks.FirstOrDefault(x => x.Value == invalidBlockHash);
					await DeleteBlockAsync(invalidBlockHash);
					WalletBlocks.Remove(elem.Key);
					ProcessedBlocks.TryRemove(invalidBlockHash, out _);
					if (elem.Key != default(Height))
					{
						foreach (var toRemove in Coins.Where(x => x.Height == elem.Key).Distinct().ToList())
						{
							RemoveCoinRecursively(toRemove);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<WalletService>(ex);
			}
		}

		private void RemoveCoinRecursively(SmartCoin toRemove)
		{
			if (toRemove.SpenderTransactionId != null)
			{
				foreach (var toAlsoRemove in Coins.Where(x => x.TransactionId == toRemove.SpenderTransactionId).Distinct().ToList())
				{
					RemoveCoinRecursively(toAlsoRemove);
				}
			}

			Coins.TryRemove(toRemove);
		}

		private async void IndexDownloader_NewFilterAsync(object sender, FilterModel filterModel)
		{
			try
			{
				using (HandleFiltersLock.Lock())
				using (WalletBlocksLock.Lock())
				{
					if (filterModel.Filter != null && !WalletBlocks.ContainsValue(filterModel.BlockHash))
					{
						await ProcessFilterModelAsync(filterModel, CancellationToken.None);
					}
				}
				NewFilterProcessed?.Invoke(this, filterModel);

				// Try perform mempool cleanup based on connected nodes' mempools.
				if (Synchronizer != null && Synchronizer.GetFiltersLeft() == 0)
				{
					MemPool?.TryPerformMempoolCleanupAsync(Nodes, CancellationToken.None);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<WalletService>(ex);
			}
		}

		public async Task InitializeAsync(CancellationToken cancel)
		{
			if (!Synchronizer.IsRunning)
			{
				throw new NotSupportedException($"{nameof(Synchronizer)} is not running.");
			}

			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				// Go through the filters and que to download the matches.
				foreach (FilterModel filterModel in Synchronizer.GetFilters().Where(x => !(x.Filter is null) && !WalletBlocks.ContainsValue(x.BlockHash))) // Filter can be null if there is no bech32 tx.
				{
					await ProcessFilterModelAsync(filterModel, cancel);
				}

				// Load in dummy mempool
				if (File.Exists(TransactionsFilePath))
				{
					var deleteTxFile = false;
					try
					{
						string jsonString = File.ReadAllText(TransactionsFilePath, Encoding.UTF8);
						var serializedTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString);

						foreach (SmartTransaction tx in serializedTransactions.Where(x => !x.Confirmed && !TransactionCache.Contains(x)).OrderBy(x => x.Height).ThenBy(x => x.FirstSeenIfMemPoolTime ?? DateTime.UtcNow))
						{
							try
							{
								await SendTransactionAsync(tx);
							}
							catch (Exception ex)
							{
								deleteTxFile = true;
								Logger.LogWarning<WalletService>(ex);
							}
						}
					}
					catch (Exception ex)
					{
						deleteTxFile = true;
						Logger.LogWarning<WalletService>(ex);
					}

					if (deleteTxFile)
					{
						try
						{
							File.Delete(TransactionsFilePath);
						}
						catch (Exception ex)
						{
							// Don't fail because of this. It's not important.
							Logger.LogWarning<WalletService>(ex);
						}
					}
				}
			}
			Coins.CollectionChanged += Coins_CollectionChanged;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			RefreshCoinsHistoriesAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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

			Block currentBlock = await GetOrDownloadBlockAsync(filterModel.BlockHash, cancel); // Wait until not downloaded.

			WalletBlocks.AddOrReplace(filterModel.BlockHeight, filterModel.BlockHash);

			if (currentBlock.GetHash() == WalletBlocks.Last().Value) // If this is the latest block then no need for deep gothrough.
			{
				ProcessBlock(filterModel.BlockHeight, currentBlock);
			}
			else // must go through all the blocks in order
			{
				foreach (var blockRef in WalletBlocks)
				{
					var block = await GetOrDownloadBlockAsync(blockRef.Value, CancellationToken.None);
					ProcessedBlocks.Clear();
					Coins.Clear();
					ProcessBlock(blockRef.Key, block);
				}
			}
		}

		public HdPubKey GetReceiveKey(string label, IEnumerable<HdPubKey> dontTouch = null)
		{
			label = Guard.Correct(label);

			// Make sure there's always 21 clean keys generated and indexed.
			KeyManager.AssertCleanKeysIndexed(21, false);

			IEnumerable<HdPubKey> keys = KeyManager.GetKeys(KeyState.Clean, isInternal: false);
			if (dontTouch != null)
			{
				keys = keys.Except(dontTouch);
				if (!keys.Any())
				{
					throw new InvalidOperationException($"{nameof(dontTouch)} covers all the possible keys.");
				}
			}

			var foundLabelless = keys.FirstOrDefault(x => !x.HasLabel); // Return the first labelless.
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

			// If the script is the same then we have a match, no matter of the anonimity set.
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

			// If it spends someone and hasn't been sufficiently anonymized.
			if (coin.AnonymitySet < ServiceConfiguration.PrivacyLevelStrong)
			{
				var c = lookupSpenderTransactionId[coin.TransactionId].Where(x => !clusters.Contains(x)).FirstOrDefault();
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

			// If it's being spent by someone and that someone hasn't been sufficiently anonymized.
			if (!coin.Unspent)
			{
				var c = lookupTransactionId[coin.SpenderTransactionId].Where(x => !clusters.Contains(x)).FirstOrDefault();
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

		private void ProcessBlock(Height height, Block block)
		{
			foreach (var tx in block.Transactions)
			{
				ProcessTransaction(new SmartTransaction(tx, height));
			}

			ProcessedBlocks.TryAdd(block.GetHash(), (height, block.Header.BlockTime));

			NewBlockProcessed?.Invoke(this, block);
		}

		private void ProcessTransaction(SmartTransaction tx)
		{
			uint256 txId = tx.GetHash();

			bool justUpdate = false;
			if (tx.Height.Type == HeightType.Chain)
			{
				MemPool.TransactionHashes.TryRemove(txId); // If we have in mempool, remove.
				if (!tx.Transaction.PossiblyNativeSegWitInvolved()) return; // We don't care about non-witness transactions for other than mempool cleanup.

				bool isFoundTx = TransactionCache.Contains(tx); // If we have in cache, update height.
				if (isFoundTx)
				{
					SmartTransaction foundTx = TransactionCache.FirstOrDefault(x => x == tx);
					if (foundTx != default(SmartTransaction)) // Must check again, because it's a concurrent collection!
					{
						foundTx.SetHeight(tx.Height);
						justUpdate = true; // No need to check for double spend, we already processed this transaction, just update it.
					}
				}
			}
			else if (!tx.Transaction.PossiblyNativeSegWitInvolved())
			{
				return; // We don't care about non-witness transactions for other than mempool cleanup.
			}

			if (!justUpdate && !tx.Transaction.IsCoinBase) // Transactions we already have and processed would be "double spends" but they shouldn't.
			{
				var doubleSpends = new List<SmartCoin>();
				foreach (SmartCoin coin in Coins)
				{
					var spent = false;
					foreach (TxoRef spentOutput in coin.SpentOutputs)
					{
						foreach (TxIn txin in tx.Transaction.Inputs)
						{
							if (spentOutput.TransactionId == txin.PrevOut.Hash && spentOutput.Index == txin.PrevOut.N) // Don't do (spentOutput == txin.PrevOut), it's faster this way, because it won't check for null.
							{
								doubleSpends.Add(coin);
								spent = true;
								break;
							}
						}
						if (spent) break;
					}
				}

				if (doubleSpends.Any())
				{
					if (tx.Height == Height.MemPool)
					{
						// if all double spent coins are mempool and RBF
						if (doubleSpends.All(x => x.RBF && x.Height == Height.MemPool))
						{
							// remove double spent coins (if other coin spends it, remove that too and so on)
							// will add later if they came to our keys
							foreach (SmartCoin doubleSpentCoin in doubleSpends)
							{
								RemoveCoinRecursively(doubleSpentCoin);
							}
						}
						else
						{
							return;
						}
					}
					else // new confirmation always enjoys priority
					{
						// remove double spent coins recursively (if other coin spends it, remove that too and so on), will add later if they came to our keys
						foreach (SmartCoin doubleSpentCoin in doubleSpends)
						{
							RemoveCoinRecursively(doubleSpentCoin);
						}
					}
				}
			}

			for (var i = 0U; i < tx.Transaction.Outputs.Count; i++)
			{
				// If transaction received to any of the wallet keys:
				var output = tx.Transaction.Outputs[i];
				HdPubKey foundKey = KeyManager.GetKeyForScriptPubKey(output.ScriptPubKey);
				if (foundKey != default)
				{
					foundKey.SetKeyState(KeyState.Used, KeyManager);
					List<SmartCoin> spentOwnCoins = Coins.Where(x => tx.Transaction.Inputs.Any(y => y.PrevOut.Hash == x.TransactionId && y.PrevOut.N == x.Index)).ToList();
					var anonset = tx.Transaction.GetAnonymitySet(i);
					if (spentOwnCoins.Count != 0)
					{
						anonset += spentOwnCoins.Min(x => x.AnonymitySet) - 1; // Minus 1, because don't count own.

						// Cleanup exposed links where the txo has been spent.
						foreach (var input in spentOwnCoins.Select(x => x.GetTxoRef()))
						{
							ChaumianClient.ExposedLinks.TryRemove(input, out _);
						}
					}

					SmartCoin newCoin = new SmartCoin(txId, i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToTxoRefs().ToArray(), tx.Height, tx.Transaction.RBF, anonset, foundKey.Label, spenderTransactionId: null, false, pubKey: foundKey); // Don't inherit locked status from key, that's different.
																																																															 // If we didn't have it.
					if (Coins.TryAdd(newCoin))
					{
						TransactionCache.TryAdd(tx);

						// If it's being mixed and anonset is not sufficient, then queue it.
						if (newCoin.Unspent && !newCoin.IsDust && ChaumianClient.HasIngredients && newCoin.Label.StartsWith("ZeroLink", StringComparison.Ordinal) && newCoin.AnonymitySet < ServiceConfiguration.MixUntilAnonymitySet)
						{
							Task.Run(async () =>
							{
								try
								{
									await ChaumianClient.QueueCoinsToMixAsync(newCoin);
								}
								catch (Exception ex)
								{
									Logger.LogError<WalletService>(ex);
								}
							});
						}

						// Make sure there's always 21 clean keys generated and indexed.
						KeyManager.AssertCleanKeysIndexed(21, foundKey.IsInternal);

						if (foundKey.IsInternal)
						{
							// Make sure there's always 14 internal locked keys generated and indexed.
							KeyManager.AssertLockedInternalKeysIndexed(14);
						}
					}
					else // If we had this coin already.
					{
						if (newCoin.Height != Height.MemPool) // Update the height of this old coin we already had.
						{
							SmartCoin oldCoin = Coins.FirstOrDefault(x => x.TransactionId == txId && x.Index == i);
							if (oldCoin != null) // Just to be sure, it is a concurrent collection.
							{
								oldCoin.Height = newCoin.Height;
							}
						}
					}
				}
			}

			// If spends any of our coin
			for (var i = 0; i < tx.Transaction.Inputs.Count; i++)
			{
				var input = tx.Transaction.Inputs[i];

				var foundCoin = Coins.FirstOrDefault(x => x.TransactionId == input.PrevOut.Hash && x.Index == input.PrevOut.N);
				if (foundCoin != null)
				{
					foundCoin.SpenderTransactionId = txId;
					TransactionCache.TryAdd(tx);
					CoinSpentOrSpenderConfirmed?.Invoke(this, foundCoin);
				}
			}
		}

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

		/// <exception cref="OperationCanceledException"></exception>
		public async Task<Block> GetOrDownloadBlockAsync(uint256 hash, CancellationToken cancel)
		{
			// Try get the block
			using (await BlockFolderLock.LockAsync())
			{
				var encoder = new HexEncoder();
				foreach (var filePath in Directory.EnumerateFiles(BlocksFolderPath))
				{
					var fileName = Path.GetFileName(filePath);
					if (!encoder.IsValid(fileName))
					{
						Logger.LogTrace<WalletService>($"Filename is not a hash: {fileName}.");
						continue;
					}

					if (hash == new uint256(fileName))
					{
						var blockBytes = await File.ReadAllBytesAsync(filePath);
						return Block.Load(blockBytes, Synchronizer.Network);
					}
				}
			}
			cancel.ThrowIfCancellationRequested();

			// Download the block
			Block block = null;
			using (await BlockDownloadLock.LockAsync())
			{
				while (true)
				{
					cancel.ThrowIfCancellationRequested();
					try
					{
						// Try to get block information from local running Core node first.
						try
						{
							if (LocalBitcoinCoreNode is null || !LocalBitcoinCoreNode.IsConnected)
							{
								DisconnectDisposeNullLocalBitcoinCoreNode();
								using (var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
								{
									handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
									var nodeConnectionParameters = new NodeConnectionParameters()
									{
										ConnectCancellation = handshakeTimeout.Token,
										IsRelay = false
									};

									var localIpEndPoint = ServiceConfiguration.BitcoinCoreEndPoint;
									var localNode = Node.Connect(Network, localIpEndPoint, nodeConnectionParameters);
									try
									{
										Logger.LogInfo<WalletService>($"TCP Connection succeeded, handshaking...");
										localNode.VersionHandshake(Constants.LocalNodeRequirements, handshakeTimeout.Token);
										var peerServices = localNode.PeerVersion.Services;

										//if(!peerServices.HasFlag(NodeServices.Network) && !peerServices.HasFlag(NodeServices.NODE_NETWORK_LIMITED))
										//{
										//	throw new InvalidOperationException($"Wasabi cannot use the local node because it doesn't provide blocks.");
										//}

										Logger.LogInfo<WalletService>($"Handshake completed successfully.");

										if (!localNode.IsConnected)
										{
											throw new InvalidOperationException($"Wasabi could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
												$"Probably this is because the node doesn't support retrieving full blocks or segwit serialization.");
										}
										LocalBitcoinCoreNode = localNode;
									}
									catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested)
									{
										Logger.LogWarning<WalletService>($"Wasabi could not complete the handshake with the local node. Probably Wasabi is not whitelisted by the node.{Environment.NewLine}" +
											$"Use \"whitebind\" or \"whitelist\" in the node configuration. (Typically whitelist=127.0.0.1 if Wasabi and the node are on the same machine.)");
										throw;
									}
								}
							}

							Block blockFromLocalNode = null;
							// Should timeout faster. Not sure if it should ever fail though. Maybe let's keep like this later for remote node connection.
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(64))) // 1/2 ADSL	512 kbit/s	00:00:32
							{
								blockFromLocalNode = LocalBitcoinCoreNode.GetBlocks(new uint256[] { hash }, cts.Token)?.Single();
							}

							if (blockFromLocalNode is null)
							{
								throw new InvalidOperationException($"Disconnected local node, because couldn't parse received block.");
							}
							else if (!blockFromLocalNode.Check())
							{
								throw new InvalidOperationException($"Disconnected node, because block invalid block received!");
							}

							block = blockFromLocalNode;
							Logger.LogInfo<WalletService>($"Block acquired from local P2P connection: {hash}");
							break;
						}
						catch (Exception ex)
						{
							block = null;
							DisconnectDisposeNullLocalBitcoinCoreNode();

							if (ex is SocketException)
							{
								Logger.LogTrace<WalletService>("Didn't find local listening and running full node instance. Trying to fetch needed block from other source.");
							}
							else
							{
								Logger.LogWarning<WalletService>(ex);
							}
						}
						cancel.ThrowIfCancellationRequested();

						// If no connection, wait then continue.
						while (Nodes.ConnectedNodes.Count == 0)
						{
							await Task.Delay(100);
						}

						Node node = Nodes.ConnectedNodes.RandomElement();
						if (node == default(Node))
						{
							await Task.Delay(100);
							continue;
						}

						if (!node.IsConnected && !(Synchronizer.Network != Network.RegTest))
						{
							await Task.Delay(100);
							continue;
						}

						try
						{
							Interlocked.Increment(ref _concurrentBlockDownload);
							ConcurrentBlockDownloadNumberChanged?.Invoke(this, _concurrentBlockDownload);

							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(64))) // 1/2 ADSL	512 kbit/s	00:00:32
							{
								block = node.GetBlocks(new uint256[] { hash }, cts.Token)?.Single();
							}

							if (block is null)
							{
								Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because couldn't parse received block.");
								node.DisconnectAsync("Couldn't parse block.");
								continue;
							}

							if (!block.Check())
							{
								Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because block invalid block received.");
								node.DisconnectAsync("Invalid block received.");
								continue;
							}

							if (Nodes.ConnectedNodes.Count > 1) // So to minimize risking missing unconfirmed transactions.
							{
								Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}. Block downloaded: {block.GetHash()}");
								node.DisconnectAsync("Thank you!");
							}
						}
						catch (TimeoutException)
						{
							Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (OperationCanceledException)
						{
							Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (Exception ex)
						{
							Logger.LogDebug<WalletService>(ex);
							Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}");
							node.DisconnectAsync("Block download failed.");
							continue;
						}
						finally
						{
							Interlocked.Decrement(ref _concurrentBlockDownload);
							ConcurrentBlockDownloadNumberChanged?.Invoke(this, _concurrentBlockDownload);
						}

						break; // If got this far break, then we have the block, it's valid. Break.
					}
					catch (Exception ex)
					{
						Logger.LogDebug<WalletService>(ex);
					}
				}
			}
			// Save the block
			using (await BlockFolderLock.LockAsync())
			{
				var path = Path.Combine(BlocksFolderPath, hash.ToString());
				await File.WriteAllBytesAsync(path, block.ToBytes());
			}

			return block;
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
					Logger.LogDebug<WalletService>(ex);
				}
				finally
				{
					try
					{
						LocalBitcoinCoreNode?.Dispose();
					}
					catch (Exception ex)
					{
						Logger.LogDebug<WalletService>(ex);
					}
					finally
					{
						LocalBitcoinCoreNode = null;
						try
						{
							Logger.LogInfo<WalletService>("Local Bitcoin Core disconnected.");
						}
						catch (Exception)
						{
							throw;
						}
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
					var fileNames = filePaths.Select(x => Path.GetFileName(x));
					var hashes = fileNames.Select(x => new uint256(x));

					if (hashes.Contains(hash))
					{
						File.Delete(Path.Combine(BlocksFolderPath, hash.ToString()));
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<WalletService>(ex);
			}
		}

		public async Task<int> CountBlocksAsync()
		{
			using (await BlockFolderLock.LockAsync())
			{
				return Directory.EnumerateFiles(BlocksFolderPath).Count();
			}
		}

		public class Operation
		{
			public Script Script { get; }
			public Money Amount { get; }
			public string Label { get; }

			public Operation(Script script, Money amount, string label)
			{
				Script = Guard.NotNull(nameof(script), script);
				Amount = Guard.NotNull(nameof(amount), amount);
				Label = label ?? "";
			}
		}

		/// <param name="toSend">If Money.Zero then spends all available amount. Doesn't generate change.</param>
		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		/// <param name="allowedInputs">Only these inputs allowed to be used to build the transaction. The wallet must know the corresponding private keys.</param>
		/// <param name="subtractFeeFromAmountIndex">If null, fee is substracted from the change. Otherwise it denotes the index in the toSend array.</param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public BuildTransactionResult BuildTransaction(string password,
														Operation[] toSend,
														int feeTarget,
														bool allowUnconfirmed = false,
														int? subtractFeeFromAmountIndex = null,
														Script customChange = null,
														IEnumerable<TxoRef> allowedInputs = null)
		{
			password = password ?? ""; // Correction.
			toSend = Guard.NotNullOrEmpty(nameof(toSend), toSend);
			if (toSend.Any(x => x is null))
			{
				throw new ArgumentNullException($"{nameof(toSend)} cannot contain null element.");
			}
			if (toSend.Any(x => x.Amount < Money.Zero))
			{
				throw new ArgumentException($"{nameof(toSend)} cannot contain negative element.");
			}

			long sum = toSend.Select(x => x.Amount).Sum().Satoshi;
			if (sum < 0 || sum > Constants.MaximumNumberOfSatoshis)
			{
				throw new ArgumentOutOfRangeException($"{nameof(toSend)} sum cannot be smaller than 0 or greater than {Constants.MaximumNumberOfSatoshis}.");
			}

			int spendAllCount = toSend.Count(x => x.Amount == Money.Zero);
			if (spendAllCount > 1)
			{
				throw new ArgumentException($"Only one {nameof(toSend)} element can contain Money.Zero. Money.Zero means add the change to the value of this output.");
			}
			if (spendAllCount == 1 && !(customChange is null))
			{
				throw new ArgumentException($"{nameof(customChange)} and send all to destination cannot be specified the same time.");
			}
			Guard.InRangeAndNotNull(nameof(feeTarget), feeTarget, 0, 1008); // Allow 0 and 1, and correct later.
			if (feeTarget < 2) // Correct 0 and 1 to 2.
			{
				feeTarget = 2;
			}
			if (subtractFeeFromAmountIndex != null) // If not null, make sure not out of range. If null fee is substracted from the change.
			{
				if (subtractFeeFromAmountIndex < 0)
				{
					throw new ArgumentOutOfRangeException($"{nameof(subtractFeeFromAmountIndex)} cannot be smaller than 0.");
				}
				if (subtractFeeFromAmountIndex > toSend.Length - 1)
				{
					throw new ArgumentOutOfRangeException($"{nameof(subtractFeeFromAmountIndex)} can be maximum {nameof(toSend)}.Length - 1. {nameof(subtractFeeFromAmountIndex)}: {subtractFeeFromAmountIndex}, {nameof(toSend)}.Length - 1: {toSend.Length - 1}.");
				}
			}

			// Get allowed coins to spend.
			List<SmartCoin> allowedSmartCoinInputs; // Inputs those can be used to build the transaction.
			if (allowedInputs != null) // If allowedInputs are specified then select the coins from them.
			{
				if (!allowedInputs.Any())
				{
					throw new ArgumentException($"{nameof(allowedInputs)} is not null, but empty.");
				}

				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.Unavailable && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.Unavailable && x.Confirmed && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();
				}
			}
			else
			{
				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.Unavailable).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.Unavailable && x.Confirmed).ToList();
				}
			}

			// 4. Get and calculate fee
			Logger.LogInfo<WalletService>("Calculating dynamic transaction fee...");

			Money feePerBytes = null;
			using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				Money feeRate = Synchronizer.GetFeeRate(feeTarget);
				Money sanityCheckedFeeRate = Math.Max(feeRate, 2); // Use the sanity check that under 2 satoshi per bytes should not be displayed. To correct possible rounding errors.
				feePerBytes = new Money(sanityCheckedFeeRate);
			}

			bool spendAll = spendAllCount == 1;
			int inNum;
			if (spendAll)
			{
				inNum = allowedSmartCoinInputs.Count;
			}
			else
			{
				int expectedMinTxSize = 1 * Constants.P2wpkhInputSizeInBytes + 1 * Constants.OutputSizeInBytes + 10;
				inNum = SelectCoinsToSpend(allowedSmartCoinInputs, toSend.Select(x => x.Amount).Sum() + feePerBytes * expectedMinTxSize).Count();
			}

			// https://bitcoincore.org/en/segwit_wallet_dev/#transaction-fee-estimation
			// https://bitcoin.stackexchange.com/a/46379/26859
			int outNum = spendAll ? toSend.Length : toSend.Length + 1; // number of addresses to send + 1 for change
			int vSize = NBitcoinHelpers.CalculateVsizeAssumeSegwit(inNum, outNum);
			Logger.LogInfo<WalletService>($"Estimated tx size: {vSize} vbytes.");
			Money fee = feePerBytes * vSize;
			Logger.LogInfo<WalletService>($"Fee: {fee.Satoshi} Satoshi.");

			// 5. How much to spend?
			long toSendAmountSumInSatoshis = toSend.Select(x => x.Amount).Sum(); // Does it work if I simply go with Money class here? Is that copied by reference of value?
			var realToSend = new (Script script, Money amount, string label)[toSend.Length];
			for (int i = 0; i < toSend.Length; i++) // clone
			{
				realToSend[i] = (
					new Script(toSend[i].Script.ToString()),
					new Money(toSend[i].Amount.Satoshi),
					toSend[i].Label);
			}
			for (int i = 0; i < realToSend.Length; i++)
			{
				if (realToSend[i].amount == Money.Zero) // means spend all
				{
					realToSend[i].amount = allowedSmartCoinInputs.Select(x => x.Amount).Sum();

					realToSend[i].amount -= new Money(toSendAmountSumInSatoshis);

					if (subtractFeeFromAmountIndex is null)
					{
						realToSend[i].amount -= fee;
					}
				}

				if (subtractFeeFromAmountIndex == i)
				{
					realToSend[i].amount -= fee;
				}

				if (realToSend[i].amount < Money.Zero)
				{
					throw new InsufficientBalanceException(fee + Money.Satoshis(1), realToSend[i].amount + fee);
				}
			}

			var toRemoveList = new List<(Script script, Money money, string label)>(realToSend);
			toRemoveList.RemoveAll(x => x.money == Money.Zero);
			realToSend = toRemoveList.ToArray();

			// 1. Get the possible changes.
			Script changeScriptPubKey;
			var sb = new StringBuilder();
			foreach (var item in realToSend)
			{
				sb.Append(item.label ?? "?");
				sb.Append(", ");
			}
			var changeLabel = $"{Constants.ChangeOfSpecialLabelStart}{sb.ToString().TrimEnd(',', ' ')}{Constants.ChangeOfSpecialLabelEnd}";

			if (customChange is null)
			{
				KeyManager.AssertCleanKeysIndexed(21, true);
				KeyManager.AssertLockedInternalKeysIndexed(14);
				var changeHdPubKey = KeyManager.GetKeys(KeyState.Clean, true).RandomElement();

				changeHdPubKey.SetLabel(changeLabel, KeyManager);
				changeScriptPubKey = changeHdPubKey.P2wpkhScript;
			}
			else
			{
				changeScriptPubKey = customChange;
			}

			// 6. Do some checks
			Money totalOutgoingAmountNoFee = realToSend.Select(x => x.amount).Sum();
			Money totalOutgoingAmount = totalOutgoingAmountNoFee + fee;
			decimal feePc = (100 * fee.ToDecimal(MoneyUnit.BTC)) / totalOutgoingAmountNoFee.ToDecimal(MoneyUnit.BTC);

			if (feePc > 1)
			{
				Logger.LogInfo<WalletService>($"The transaction fee is {feePc:0.#}% of your transaction amount."
					+ Environment.NewLine + $"Sending:\t {totalOutgoingAmount.ToString(fplus: false, trimExcessZero: true)} BTC."
					+ Environment.NewLine + $"Fee:\t\t {fee.Satoshi} Satoshi.");
			}
			if (feePc > 100)
			{
				throw new InvalidOperationException($"The transaction fee is more than twice as much as your transaction amount: {feePc:0.#}%.");
			}

			var confirmedAvailableAmount = allowedSmartCoinInputs.Where(x => x.Confirmed).Select(x => x.Amount).Sum();
			var spendsUnconfirmed = false;
			if (confirmedAvailableAmount < totalOutgoingAmount)
			{
				spendsUnconfirmed = true;
				Logger.LogInfo<WalletService>("Unconfirmed transaction are being spent.");
			}

			// 7. Select coins
			Logger.LogInfo<WalletService>("Selecting coins...");
			IEnumerable<SmartCoin> coinsToSpend = SelectCoinsToSpend(allowedSmartCoinInputs, totalOutgoingAmount);

			// 8. Get signing keys
			IEnumerable<ExtKey> signingKeys = KeyManager.GetSecrets(password, coinsToSpend.Select(x => x.ScriptPubKey).ToArray());

			// 9. Build the transaction
			Logger.LogInfo<WalletService>("Signing transaction...");
			var builder = Network.CreateTransactionBuilder();
			builder = builder
				.AddCoins(coinsToSpend.Select(x => x.GetCoin()))
				.AddKeys(signingKeys.ToArray());

			foreach ((Script scriptPubKey, Money amount, string label) output in realToSend)
			{
				builder = builder.Send(output.scriptPubKey, output.amount);
			}

			var tx = builder
				.SetChange(changeScriptPubKey)
				.SendFees(fee)
				.BuildTransaction(true);

			TransactionPolicyError[] checkResults = builder.Check(tx, fee);
			if (checkResults.Length > 0)
			{
				throw new InvalidTxException(tx, checkResults);
			}

			List<SmartCoin> spentCoins = Coins.Where(x => tx.Inputs.Any(y => y.PrevOut.Hash == x.TransactionId && y.PrevOut.N == x.Index)).ToList();

			TxoRef[] spentOutputs = spentCoins.Select(x => new TxoRef(x.TransactionId, x.Index)).ToArray();

			var outerWalletOutputs = new List<SmartCoin>();
			var innerWalletOutputs = new List<SmartCoin>();
			for (var i = 0U; i < tx.Outputs.Count; i++)
			{
				TxOut output = tx.Outputs[i];
				var anonset = (tx.GetAnonymitySet(i) + spentCoins.Min(x => x.AnonymitySet)) - 1; // Minus 1, because count own only once.
				var foundKey = KeyManager.GetKeys(KeyState.Clean).FirstOrDefault(x => output.ScriptPubKey == x.P2wpkhScript);
				var coin = new SmartCoin(tx.GetHash(), i, output.ScriptPubKey, output.Value, tx.Inputs.ToTxoRefs().ToArray(), Height.Unknown, tx.RBF, anonset, pubKey: foundKey);

				if (foundKey != null)
				{
					coin.Label = changeLabel;
					innerWalletOutputs.Add(coin);
				}
				else
				{
					outerWalletOutputs.Add(coin);
				}
			}

			Logger.LogInfo<WalletService>($"Transaction is successfully built: {tx.GetHash()}.");

			return new BuildTransactionResult(new SmartTransaction(tx, Height.Unknown), spendsUnconfirmed, fee, feePc, outerWalletOutputs, innerWalletOutputs, spentCoins);
		}

		private IEnumerable<SmartCoin> SelectCoinsToSpend(IEnumerable<SmartCoin> unspentCoins, Money totalOutAmount)
		{
			var coinsToSpend = new HashSet<SmartCoin>();
			var unspentConfirmedCoins = new List<SmartCoin>();
			var unspentUnconfirmedCoins = new List<SmartCoin>();
			foreach (SmartCoin coin in unspentCoins)
			{
				if (coin.Confirmed)
				{
					unspentConfirmedCoins.Add(coin);
				}
				else
				{
					unspentUnconfirmedCoins.Add(coin);
				}
			}

			bool haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentConfirmedCoins);
			if (!haveEnough)
			{
				haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentUnconfirmedCoins);
			}

			if (!haveEnough)
			{
				throw new InsufficientBalanceException(totalOutAmount, unspentConfirmedCoins.Select(x => x.Amount).Sum() + unspentUnconfirmedCoins.Select(x => x.Amount).Sum());
			}

			return coinsToSpend;
		}

		private bool SelectCoins(ref HashSet<SmartCoin> coinsToSpend, Money totalOutAmount, IEnumerable<SmartCoin> unspentCoins)
		{
			var haveEnough = false;
			foreach (var coin in unspentCoins.OrderByDescending(x => x.Amount))
			{
				coinsToSpend.Add(coin);
				// if doesn't reach amount, continue adding next coin
				if (coinsToSpend.Select(x => x.Amount).Sum() < totalOutAmount) continue;

				haveEnough = true;
				break;
			}

			return haveEnough;
		}

		public void RenameLabel(SmartCoin coin, string newLabel)
		{
			newLabel = Guard.Correct(newLabel);
			coin.Label = newLabel;
			var key = KeyManager.GetKeys(x => x.P2wpkhScript == coin.ScriptPubKey).SingleOrDefault();
			if (key != null)
			{
				key.SetLabel(newLabel, KeyManager);
			}
		}

		public async Task SendTransactionAsync(SmartTransaction transaction)
		{
			using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				try
				{
					await client.BroadcastAsync(transaction);
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("txn-mempool-conflict", StringComparison.InvariantCultureIgnoreCase))
				{
					if (transaction.Transaction.Inputs.Count == 1)
					{
						OutPoint input = transaction.Transaction.Inputs.First().PrevOut;
						SmartCoin coin = Coins.FirstOrDefault(x => x.TransactionId == input.Hash && x.Index == input.N);
						if (coin != default)
						{
							coin.SpentAccordingToBackend = true;
						}
					}
					throw;
				}
			}

			ProcessTransaction(new SmartTransaction(transaction.Transaction, Height.MemPool));
			MemPool.TransactionHashes.TryAdd(transaction.GetHash());

			Logger.LogInfo<WalletService>($"Transaction is successfully broadcasted: {transaction.GetHash()}.");
		}

		public IEnumerable<string> GetNonSpecialLabels()
		{
			return Coins.Where(x => !x.Label.StartsWith("ZeroLink", StringComparison.Ordinal))
				.SelectMany(x => x.Label.Split(new string[] { Constants.ChangeOfSpecialLabelStart, Constants.ChangeOfSpecialLabelEnd, "(", "," }, StringSplitOptions.RemoveEmptyEntries))
				.Select(x => x.Trim())
				.Distinct();
		}

		private long _refreshCoinCalls;

		public async Task RefreshCoinsHistoriesAsync()
		{
			try
			{
				if (Interlocked.Read(ref _refreshCoinCalls) == 2) //it is running and scheduled to rerun after finished
				{
					return;
				}
				if (Interlocked.Read(ref _refreshCoinCalls) == 1) //it is running but now we will rerun if finished
				{
					Interlocked.Increment(ref _refreshCoinCalls);
					return;
				}
				if (Interlocked.Read(ref _refreshCoinCalls) == 0) //it is not running so we start the work
				{
					Interlocked.Increment(ref _refreshCoinCalls);
				}
				var unspentCoins = Coins.Where(c => c.Unspent && !c.IsDust); //refreshing unspent coins clusters only
				if (unspentCoins.Any())
				{
					ILookup<Script, SmartCoin> lookupScriptPubKey = Coins.ToLookup(c => c.ScriptPubKey, c => c);
					ILookup<uint256, SmartCoin> lookupSpenderTransactionId = Coins.ToLookup(c => c.SpenderTransactionId, c => c);
					ILookup<uint256, SmartCoin> lookupTransactionId = Coins.ToLookup(c => c.TransactionId, c => c);

					const int simultaneousThread = 2; //threads allowed to run simultaneously in threadpool

					await Task.Run(() => Parallel.ForEach(unspentCoins, new ParallelOptions { MaxDegreeOfParallelism = simultaneousThread }, coin =>
					{
						var result = string.Join(", ", GetClusters(coin, new List<SmartCoin>(), lookupScriptPubKey, lookupSpenderTransactionId, lookupTransactionId).Select(x => x.Label).Distinct());
						coin.SetClusters(result);
					}));
				}
				if (Interlocked.Read(ref _refreshCoinCalls) == 2) //scheduled to rerun so we start the work again
				{
					Interlocked.Exchange(ref _refreshCoinCalls, 0);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
					RefreshCoinsHistoriesAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				}
				if (Interlocked.Read(ref _refreshCoinCalls) == 1) //done with the job
				{
					Interlocked.Exchange(ref _refreshCoinCalls, 0);
				}
			}
			catch (Exception ex)
			{
				Interlocked.Exchange(ref _refreshCoinCalls, 0);
				Logger.LogError<WalletService>($"Refreshing coin clusters failed: {ex}");
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls
		private Node _localBitcoinCoreNode = null;

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Synchronizer.NewFilter -= IndexDownloader_NewFilterAsync;
					Synchronizer.Reorged -= IndexDownloader_ReorgedAsync;
					MemPool.TransactionReceived -= MemPool_TransactionReceived;
					Coins.CollectionChanged -= Coins_CollectionChanged;

					IoHelpers.EnsureContainingDirectoryExists(TransactionsFilePath);
					string jsonString = JsonConvert.SerializeObject(TransactionCache.OrderBy(x => x.Height).ThenBy(x => x.FirstSeenIfMemPoolTime ?? DateTime.UtcNow), Formatting.Indented);
					File.WriteAllText(TransactionsFilePath,
						jsonString,
						Encoding.UTF8);

					DisconnectDisposeNullLocalBitcoinCoreNode();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
