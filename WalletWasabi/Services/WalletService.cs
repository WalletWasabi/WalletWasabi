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

		public ConcurrentHashSet<SmartTransaction> TransactionCache { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<SmartCoin> CoinSpentOrSpenderConfirmed;

		public event EventHandler<Block> NewBlockProcessed;

		public Network Network => Synchronizer.Network;

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
			TransactionCache = new ConcurrentHashSet<SmartTransaction>();

			BlocksFolderPath = Path.Combine(workFolderDir, "Blocks", Network.ToString());
			TransactionsFolderPath = Path.Combine(workFolderDir, "Transactions", Network.ToString());
			RuntimeParams.SetDataDir(workFolderDir);

			BlockFolderLock = new AsyncLock();

			KeyManager.AssertCleanKeysIndexed();
			KeyManager.AssertLockedInternalKeysIndexed(14);

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

		private void Coins_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
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
				Logger.LogWarning<WalletService>(ex);
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
			Mempool.TransactionHashes.TryRemove(toRemove.TransactionId);
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
				Logger.LogWarning<WalletService>(ex);
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
						Logger.LogWarning<WalletService>(ex);
						Logger.LogWarning<WalletService>($"Transaction cache got corrupted. Deleting {TransactionsFilePath}.");
						File.Delete(TransactionsFilePath);
					}
				}

				// Go through the keymanager's index.
				KeyManager.AssertNetworkOrClearBlockState(Network);
				Height bestKeyManagerHeight = KeyManager.GetBestHeight();

				foreach (BlockState blockState in KeyManager.GetTransactionIndex())
				{
					var relevantTransactions = confirmedTransactions.Where(x => x.BlockHash == blockState.BlockHash).ToArray();
					var block = await GetOrDownloadBlockAsync(blockState.BlockHash, cancel);
					await ProcessBlockAsync(blockState.BlockHeight, block, blockState.TransactionIndices, relevantTransactions);
				}

				// Go through the filters and que to download the matches.
				await BitcoinStore.IndexStore.ForeachFiltersAsync(async (filterModel) =>
				{
					if (filterModel.Filter != null) // Filter can be null if there is no bech32 tx.
					{
						await ProcessFilterModelAsync(filterModel, cancel);
					}
				}, new Height(bestKeyManagerHeight.Value + 1));

				// Load in dummy mempool
				try
				{
					if (unconfirmedTransactions != null && unconfirmedTransactions.Any())
					{
						try
						{
							using (await TransactionProcessingLock.LockAsync())
							using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
							{
								var compactness = 10;

								var mempoolHashes = await client.GetMempoolHashesAsync(compactness);

								var cnt = 0;
								foreach (var tx in unconfirmedTransactions)
								{
									if (mempoolHashes.Contains(tx.GetHash().ToString().Substring(0, compactness)))
									{
										tx.SetHeight(Height.Mempool);
										await ProcessTransactionAsync(tx);
										Mempool.TransactionHashes.TryAdd(tx.GetHash());

										Logger.LogInfo<WalletService>($"Transaction was successfully tested against the backend's mempool hashes: {tx.GetHash()}.");
										cnt++;
									}
								}

								if (cnt != unconfirmedTransactions.Length)
								{
									SerializeTransactionCache();
								}
							}
						}
						catch
						{
							// When there's a connection failure do not clean the transactions, add it to processing.
							foreach (var tx in unconfirmedTransactions)
							{
								tx.SetHeight(Height.Mempool);
								await ProcessTransactionAsync(tx);
								Mempool.TransactionHashes.TryAdd(tx.GetHash());
							}

							throw;
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning<WalletService>(ex);
				}
				finally
				{
					UnconfirmedTransactionsInitialized = true;
				}
			}
			Coins.CollectionChanged += Coins_CollectionChanged;
			RefreshCoinHistories();
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

			if (await ProcessBlockAsync(filterModel.BlockHeight, currentBlock))
			{
				SerializeTransactionCache();
			}
		}

		public HdPubKey GetReceiveKey(string label, IEnumerable<HdPubKey> dontTouch = null)
		{
			label = Guard.Correct(label);

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

		private async Task<bool> ProcessBlockAsync(Height height, Block block, IEnumerable<int> filterByTxIds = null, IEnumerable<SmartTransaction> skeletonBlock = null)
		{
			var ret = false;
			using (await TransactionProcessingLock.LockAsync())
			{
				if (filterByTxIds is null)
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
						KeyManager.AddBlockState(blockState, setItsHeightToBest: true); // Set the heigh here (so less toFile and lock.)
					}
					else
					{
						KeyManager.SetBestHeight(height);
					}
				}
				else
				{
					foreach (var i in filterByTxIds.OrderBy(x => x))
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
			uint256 txId = tx.GetHash();
			var walletRelevant = false;

			bool justUpdate = false;
			if (tx.Confirmed)
			{
				Mempool.TransactionHashes.TryRemove(txId); // If we have in mempool, remove.
				if (!tx.Transaction.PossiblyP2WPKHInvolved())
				{
					return false; // We do not care about non-witness transactions for other than mempool cleanup.
				}

				bool isFoundTx = TransactionCache.Contains(tx); // If we have in cache, update height.
				if (isFoundTx)
				{
					SmartTransaction foundTx = TransactionCache.FirstOrDefault(x => x == tx);
					if (foundTx != default(SmartTransaction)) // Must check again, because it's a concurrent collection!
					{
						foundTx.SetHeight(tx.Height, tx.BlockHash, tx.BlockIndex);
						walletRelevant = true;
						justUpdate = true; // No need to check for double spend, we already processed this transaction, just update it.
					}
				}
			}
			else if (!tx.Transaction.PossiblyP2WPKHInvolved())
			{
				return false; // We do not care about non-witness transactions for other than mempool cleanup.
			}

			if (!justUpdate && !tx.Transaction.IsCoinBase) // Transactions we already have and processed would be "double spends" but they shouldn't.
			{
				var doubleSpends = new List<SmartCoin>();
				foreach (SmartCoin coin in Coins)
				{
					var spent = false;
					foreach (TxoRef spentOutput in coin.SpentOutputs)
					{
						foreach (TxIn txIn in tx.Transaction.Inputs)
						{
							if (spentOutput.TransactionId == txIn.PrevOut.Hash && spentOutput.Index == txIn.PrevOut.N) // Do not do (spentOutput == txIn.PrevOut), it's faster this way, because it won't check for null.
							{
								doubleSpends.Add(coin);
								spent = true;
								walletRelevant = true;
								break;
							}
						}
						if (spent)
						{
							break;
						}
					}
				}

				if (doubleSpends.Any())
				{
					if (tx.Height == Height.Mempool)
					{
						// if the received transaction is spending at least one input already
						// spent by a previous unconfirmed transaction signaling RBF then it is not a double
						// spanding transaction but a replacement transaction.
						if (doubleSpends.Any(x => x.IsReplaceable))
						{
							// remove double spent coins (if other coin spends it, remove that too and so on)
							// will add later if they came to our keys
							foreach (SmartCoin doubleSpentCoin in doubleSpends.Where(x => !x.Confirmed))
							{
								RemoveCoinRecursively(doubleSpentCoin);
							}
							tx.SetReplacement();
							walletRelevant = true;
						}
						else
						{
							return false;
						}
					}
					else // new confirmation always enjoys priority
					{
						// remove double spent coins recursively (if other coin spends it, remove that too and so on), will add later if they came to our keys
						foreach (SmartCoin doubleSpentCoin in doubleSpends)
						{
							RemoveCoinRecursively(doubleSpentCoin);
						}
						walletRelevant = true;
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
					walletRelevant = true;

					foundKey.SetKeyState(KeyState.Used, KeyManager);
					List<SmartCoin> spentOwnCoins = Coins.Where(x => tx.Transaction.Inputs.Any(y => y.PrevOut.Hash == x.TransactionId && y.PrevOut.N == x.Index)).ToList();
					var anonset = tx.Transaction.GetAnonymitySet(i);
					if (spentOwnCoins.Count != 0)
					{
						anonset += spentOwnCoins.Min(x => x.AnonymitySet) - 1; // Minus 1, because do not count own.

						// Cleanup exposed links where the txo has been spent.
						foreach (var input in spentOwnCoins.Select(x => x.GetTxoRef()))
						{
							ChaumianClient.ExposedLinks.TryRemove(input, out _);
						}
					}

					if (output.Value <= ServiceConfiguration.DustThreshold)
					{
						continue;
					}

					SmartCoin newCoin = new SmartCoin(txId, i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToTxoRefs().ToArray(), tx.Height, tx.IsRBF, anonset, foundKey.Label, spenderTransactionId: null, false, pubKey: foundKey); // Do not inherit locked status from key, that's different.
																																																												   // If we did not have it.
					if (Coins.TryAdd(newCoin))
					{
						TransactionCache.TryAdd(tx);

						// If it's being mixed and anonset is not sufficient, then queue it.
						if (newCoin.Unspent && ChaumianClient.HasIngredients && newCoin.AnonymitySet < ServiceConfiguration.MixUntilAnonymitySet && ChaumianClient.State.Contains(newCoin.SpentOutputs))
						{
							try
							{
								await ChaumianClient.QueueCoinsToMixAsync(newCoin);
							}
							catch (Exception ex)
							{
								Logger.LogError<WalletService>(ex);
							}
						}

						// Make sure there's always 21 clean keys generated and indexed.
						KeyManager.AssertCleanKeysIndexed(isInternal: foundKey.IsInternal);

						if (foundKey.IsInternal)
						{
							// Make sure there's always 14 internal locked keys generated and indexed.
							KeyManager.AssertLockedInternalKeysIndexed(14);
						}
					}
					else // If we had this coin already.
					{
						if (newCoin.Height != Height.Mempool) // Update the height of this old coin we already had.
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
					walletRelevant = true;

					foundCoin.SpenderTransactionId = txId;
					TransactionCache.TryAdd(tx);
					CoinSpentOrSpenderConfirmed?.Invoke(this, foundCoin);
				}
			}

			return walletRelevant;
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
						try
						{
							return Block.Load(blockBytes, Synchronizer.Network);
						}
						catch (Exception)
						{
							// In case the block file is corrupted we get an EndOfStreamException exception
							// Ignore any error and continue by re-downloading the block.
							break;
						}
					}
				}
			}
			cancel.ThrowIfCancellationRequested();

			// Download the block
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
						try
						{
							if (LocalBitcoinCoreNode is null || !LocalBitcoinCoreNode.IsConnected && Network != Network.RegTest) // If RegTest then we're already connected do not try again.
							{
								DisconnectDisposeNullLocalBitcoinCoreNode();
								using (var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
								{
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
										Logger.LogInfo<WalletService>($"TCP Connection succeeded, handshaking...");
										localNode.VersionHandshake(Constants.LocalNodeRequirements, handshakeTimeout.Token);
										var peerServices = localNode.PeerVersion.Services;

										//if (!peerServices.HasFlag(NodeServices.Network) && !peerServices.HasFlag(NodeServices.NODE_NETWORK_LIMITED))
										//{
										//	throw new InvalidOperationException($"Wasabi cannot use the local node because it does not provide blocks.");
										//}

										Logger.LogInfo<WalletService>($"Handshake completed successfully.");

										if (!localNode.IsConnected)
										{
											throw new InvalidOperationException($"Wasabi could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
												$"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
										}
										LocalBitcoinCoreNode = localNode;
									}
									catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested)
									{
										Logger.LogWarning<Node>($"Wasabi could not complete the handshake with the local node. Probably Wasabi is not whitelisted by the node.{Environment.NewLine}" +
											$"Use \"whitebind\" in the node configuration. (Typically whitebind=127.0.0.1:8333 if Wasabi and the node are on the same machine and whitelist=1.2.3.4 if they are not.)");
										throw;
									}
								}
							}

							Block blockFromLocalNode = null;
							// Should timeout faster. Not sure if it should ever fail though. Maybe let's keep like this later for remote node connection.
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(64)))
							{
								blockFromLocalNode = await LocalBitcoinCoreNode.DownloadBlockAsync(hash, cts.Token);
							}

							if (!blockFromLocalNode.Check())
							{
								throw new InvalidOperationException($"Disconnected node, because invalid block received!");
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
								Logger.LogTrace<WalletService>("Did not find local listening and running full node instance. Trying to fetch needed block from other source.");
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
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(RuntimeParams.Instance.NetworkNodeTimeout))) // 1/2 ADSL	512 kbit/s	00:00:32
							{
								block = await node.DownloadBlockAsync(hash, cts.Token);
							}

							if (!block.Check())
							{
								Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.");
								node.DisconnectAsync("Invalid block received.");
								continue;
							}

							if (Nodes.ConnectedNodes.Count > 1) // So to minimize risking missing unconfirmed transactions.
							{
								Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}. Block downloaded: {block.GetHash()}");
								node.DisconnectAsync("Thank you!");
							}

							await NodeTimeoutsAsync(false);
						}
						catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
						{
							Logger.LogInfo<WalletService>($"Disconnected node: {node.RemoteSocketAddress}, because block download took too long.");

							await NodeTimeoutsAsync(true);

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

						break; // If got this far break, then we have the block, it's valid. Break.
					}
					catch (Exception ex)
					{
						Logger.LogDebug<WalletService>(ex);
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
						Logger.LogInfo<WalletService>("Local Bitcoin Core node disconnected.");
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

		/// <param name="toSend">If Money.Zero then spends all available amount. Does not generate change.</param>
		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		/// <param name="allowedInputs">Only these inputs allowed to be used to build the transaction. The wallet must know the corresponding private keys.</param>
		/// <param name="subtractFeeFromAmountIndex">If null, fee is substracted from the change. Otherwise it denotes the index in the toSend array.</param>
		/// <param name="feeTarget">The target number of blocks to estimate the fee.</param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public BuildTransactionResult BuildTransaction(string password,
														Operation[] toSend,
														int? feeTarget = null,
														Money feeRate = null,
														bool allowUnconfirmed = false,
														int? subtractFeeFromAmountIndex = null,
														Script customChange = null,
														IEnumerable<TxoRef> allowedInputs = null)
		{
			password = password ?? ""; // Correction.
			toSend = Guard.NotNullOrEmpty(nameof(toSend), toSend);

			if ((feeRate is null && feeTarget is null)
				|| (feeRate != null && feeTarget != null))
			{
				throw new NotSupportedException($"Either {nameof(feeRate)} or {nameof(feeTarget)} must be set and the other should be null.");
			}

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
				throw new ArgumentException($"{nameof(customChange)} and send all to destination cannot be specified at the same time.");
			}

			if (feeTarget.HasValue)
			{
				Guard.InRangeAndNotNull(nameof(feeTarget), feeTarget.Value, 0, Constants.SevenDaysConfirmationTarget); // Allow 0 and 1, and correct later.

				if (feeTarget < 2) // Correct 0 and 1 to 2.
				{
					feeTarget = 2;
				}
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
			}
			else
			{
				allowedSmartCoinInputs = allowUnconfirmed ? Coins.Where(x => !x.Unavailable).ToList() : Coins.Where(x => !x.Unavailable && x.Confirmed).ToList();
			}

			// 4. Get and calculate fee
			Logger.LogInfo<WalletService>("Calculating dynamic transaction fee...");

			Money feePerBytes = null;
			using (var client = new WasabiClient(Synchronizer.WasabiClient.TorClient.DestinationUriAction, Synchronizer.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				Money fr = feeTarget.HasValue ? Synchronizer.GetFeeRate(feeTarget.Value) : feeRate;

				Money sanityCheckedFeeRate = Math.Max(fr, 2); // Use the sanity check that under 2 satoshi per bytes should not be displayed. To correct possible rounding errors.
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
				KeyManager.AssertCleanKeysIndexed(isInternal: true);
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
				Logger.LogInfo<WalletService>($"The transaction fee is {feePc:0.#}% of your transaction amount.{Environment.NewLine}"
					+ $"Sending:\t {totalOutgoingAmount.ToString(fplus: false, trimExcessZero: true)} BTC.{Environment.NewLine}"
					+ $"Fee:\t\t {fee.Satoshi} Satoshi.");
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

			// 9. Build the transaction
			Logger.LogInfo<WalletService>("Signing transaction...");
			TransactionBuilder builder = Network.CreateTransactionBuilder();
			// It must be watch only, too, because if we have the key and also hardware wallet, we do not care we can sign.
			bool sign = !KeyManager.IsWatchOnly;
			if (sign)
			{
				// 8. Get signing keys
				IEnumerable<ExtKey> signingKeys = KeyManager.GetSecrets(password, coinsToSpend.Select(x => x.ScriptPubKey).ToArray());

				builder = builder
					.AddCoins(coinsToSpend.Select(x => x.GetCoin()))
					.AddKeys(signingKeys.ToArray());
			}
			else
			{
				builder = builder
					.AddCoins(coinsToSpend.Select(x => x.GetCoin()));
			}

			foreach ((Script scriptPubKey, Money amount, string label) output in realToSend)
			{
				builder = builder.Send(output.scriptPubKey, output.amount);
			}

			Transaction tx = builder
				.SetChange(changeScriptPubKey)
				.SendFees(fee)
				.BuildTransaction(sign);

			if (sign)
			{
				TransactionPolicyError[] checkResults = builder.Check(tx, fee);
				if (checkResults.Length > 0)
				{
					throw new InvalidTxException(tx, checkResults);
				}
			}

			List<SmartCoin> spentCoins = Coins.Where(x => tx.Inputs.Any(y => y.PrevOut.Hash == x.TransactionId && y.PrevOut.N == x.Index)).ToList();

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

			PSBT psbt = builder.BuildPSBT(sign);
			HashSet<SmartCoin> allTxCoins = spentCoins.Concat(innerWalletOutputs).Concat(outerWalletOutputs).ToHashSet();
			foreach (var coin in allTxCoins)
			{
				if (coin.HdPubKey != null)
				{
					var index = -1;
					var isInput = false;
					for (int i = 0; i < tx.Inputs.Count; i++)
					{
						var input = tx.Inputs[i];
						if (input.PrevOut == coin.GetOutPoint())
						{
							index = i;
							isInput = true;
							break;
						}
					}
					if (!isInput)
					{
						index = (int)coin.Index;
					}

					if (KeyManager.MasterFingerprint.HasValue)
					{
						var rootKeyPath = new RootedKeyPath(KeyManager.MasterFingerprint.Value, coin.HdPubKey.FullKeyPath);
						psbt.AddKeyPath(coin.HdPubKey.PubKey, rootKeyPath, coin.ScriptPubKey);
					}
				}
			}

			Logger.LogInfo<WalletService>($"Transaction is successfully built: {tx.GetHash()}.");

			return new BuildTransactionResult(new SmartTransaction(tx, Height.Unknown), psbt, spendsUnconfirmed, sign, fee, feePc, outerWalletOutputs, innerWalletOutputs, spentCoins);
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

			bool haveEnough = TrySelectCoins(ref coinsToSpend, totalOutAmount, unspentConfirmedCoins);
			if (!haveEnough)
			{
				haveEnough = TrySelectCoins(ref coinsToSpend, totalOutAmount, unspentUnconfirmedCoins);
			}

			if (!haveEnough)
			{
				throw new InsufficientBalanceException(totalOutAmount, unspentConfirmedCoins.Select(x => x.Amount).Sum() + unspentUnconfirmedCoins.Select(x => x.Amount).Sum());
			}

			return coinsToSpend;
		}

		/// <returns>If the selection was successful. If there's enough coins to spend from.</returns>
		private bool TrySelectCoins(ref HashSet<SmartCoin> coinsToSpend, Money totalOutAmount, IEnumerable<SmartCoin> unspentCoins)
		{
			// If there's no need for input merging, then use the largest selected.
			// Do not prefer anonymity set. You can assume the user prefers anonymity set manually through the GUI.
			SmartCoin largestCoin = unspentCoins.OrderByDescending(x => x.Amount).FirstOrDefault();
			if (largestCoin == default)
			{
				return false; // If there's no coin then unsuccessful selection.
			}
			else // Check if we can do without input merging.
			{
				if (largestCoin.Amount >= totalOutAmount)
				{
					coinsToSpend.Add(largestCoin);
					return true;
				}
			}

			// If there's a need for input merging.
			foreach (var coin in unspentCoins
				.OrderByDescending(x => x.AnonymitySet) // Always try to spend/merge the largest anonset coins first.
				.ThenByDescending(x => x.Amount)) // Then always try to spend by amount.
			{
				coinsToSpend.Add(coin);
				// If reaches the amount, then return true, else just go with the largest coin.
				if (coinsToSpend.Select(x => x.Amount).Sum() >= totalOutAmount)
				{
					return true;
				}
			}

			return false;
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
					throw new InvalidOperationException("Transaction broadcasting to nodes does not work in RegTest.");
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

					Logger.LogInfo<WalletService>($"Trying to broadcast transaction with random node ({node.RemoteSocketAddress}):{transaction.GetHash()}");
					var addedToBroadcastStore = Mempool.TryAddToBroadcastStore(transaction.Transaction, node.RemoteSocketEndpoint.ToString()); // So we'll reply to INV with this transaction.
					if (!addedToBroadcastStore)
					{
						Logger.LogWarning<WalletService>($"Transaction {transaction.GetHash()} was already present in the broadcast store.");
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
						Logger.LogInfo<MempoolBehavior>($"Disconnected node: {node.RemoteSocketAddress}. Successfully broadcasted transaction: {transaction.GetHash()}.");

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
						Logger.LogInfo<MempoolBehavior>($"Transaction is successfully propagated: {transaction.GetHash()}.");
					}
					else
					{
						Logger.LogWarning<WalletService>($"Expected transaction {transaction.GetHash()} was not found in the broadcast store.");
					}
					break;
				}
			}
			catch (Exception ex)
			{
				Logger.LogInfo<WalletService>($"Random node could not broadcast transaction. Broadcasting with backend... Reason: {ex.Message}");
				Logger.LogDebug<WalletService>(ex);

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

				Logger.LogInfo<WalletService>($"Transaction is successfully broadcasted to backend: {transaction.GetHash()}.");
			}
			finally
			{
				Mempool.TryRemoveFromBroadcastStore(transaction.GetHash(), out _); // Remove it just to be sure. Probably has been removed previously.
				Interlocked.Decrement(ref SendCount);
			}
		}

		public IEnumerable<string> GetNonSpecialLabels()
		{
			return Coins.Where(x => !x.Label.StartsWith("ZeroLink", StringComparison.Ordinal))
				.SelectMany(x => x.Label.Split(new string[] { Constants.ChangeOfSpecialLabelStart, Constants.ChangeOfSpecialLabelEnd, "(", "," }, StringSplitOptions.RemoveEmptyEntries))
				.Select(x => x.Trim())
				.Distinct();
		}

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
						var result = string.Join(", ", GetClusters(coin, new List<SmartCoin>(), lookupScriptPubKey, lookupSpenderTransactionId, lookupTransactionId).Select(x => x.Label).Distinct());
						coin.SetClusters(result);
					});
				}
			}
			catch (Exception ex)
			{
				Logger.LogError<WalletService>($"Refreshing coin clusters failed: {ex}");
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

			Logger.LogInfo<WalletService>($"Current timeout value used on block download is: {timeout} seconds.");
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

			DisconnectDisposeNullLocalBitcoinCoreNode();
		}
	}
}
