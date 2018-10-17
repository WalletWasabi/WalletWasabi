using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
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
using System.Collections.ObjectModel;
using WalletWasabi.WebClients.Wasabi;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using NBitcoin.DataEncoders;

namespace WalletWasabi.Services
{
	public class WalletService : IDisposable
	{
		public KeyManager KeyManager { get; }
		public IndexDownloader IndexDownloader { get; }
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

		public SortedDictionary<Height, uint256> WalletBlocks { get; }
		public ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)> ProcessedBlocks { get; }
		private AsyncLock WalletBlocksLock { get; }

		public NotifyingConcurrentHashSet<SmartCoin> Coins { get; }

		public ConcurrentHashSet<SmartTransaction> TransactionCache { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;

		public event EventHandler<SmartCoin> CoinSpentOrSpenderConfirmed;

		public event EventHandler<SmartCoin> CoinReceived;

		public event EventHandler<Block> NewBlockProcessed;

		public Network Network => IndexDownloader.Network;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public WalletService(KeyManager keyManager, IndexDownloader indexDownloader, CcjClient chaumianClient, MemPoolService memPool, NodesGroup nodes, string workFolderDir)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			IndexDownloader = Guard.NotNull(nameof(indexDownloader), indexDownloader);
			ChaumianClient = Guard.NotNull(nameof(chaumianClient), chaumianClient);
			MemPool = Guard.NotNull(nameof(memPool), memPool);

			WalletBlocks = new SortedDictionary<Height, uint256>();
			ProcessedBlocks = new ConcurrentDictionary<uint256, (Height height, DateTimeOffset dateTime)>();
			WalletBlocksLock = new AsyncLock();
			HandleFiltersLock = new AsyncLock();

			Coins = new NotifyingConcurrentHashSet<SmartCoin>();
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
				if (IndexDownloader.Network == Network.RegTest)
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
				if (IndexDownloader.Network == Network.RegTest)
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

			IndexDownloader.NewFilter += IndexDownloader_NewFilterAsync;
			IndexDownloader.Reorged += IndexDownloader_ReorgedAsync;
			MemPool.TransactionReceived += MemPool_TransactionReceived;
		}

		private void MemPool_TransactionReceived(object sender, SmartTransaction tx)
		{
			ProcessTransaction(tx);
		}

		private async void IndexDownloader_ReorgedAsync(object sender, uint256 invalidBlockHash)
		{
			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				var elem = WalletBlocks.FirstOrDefault(x => x.Value == invalidBlockHash);
				await DeleteBlockAsync(invalidBlockHash);
				WalletBlocks.RemoveByValue(invalidBlockHash);
				ProcessedBlocks.TryRemove(invalidBlockHash, out _);
				if (elem.Key != default(Height))
				{
					foreach (var toRemove in Coins.Where(x => x.Height == elem.Key).ToHashSet())
					{
						RemoveCoinRecursively(toRemove);
					}
				}
			}
		}

		private void RemoveCoinRecursively(SmartCoin toRemove)
		{
			if (!(toRemove.SpenderTransactionId is null))
			{
				foreach (var toAlsoRemove in Coins.Where(x => x.TransactionId == toRemove.SpenderTransactionId).ToHashSet())
				{
					RemoveCoinRecursively(toAlsoRemove);
				}
			}

			Coins.TryRemove(toRemove);
		}

		private async void IndexDownloader_NewFilterAsync(object sender, FilterModel filterModel)
		{
			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				if (!(filterModel.Filter is null) && !WalletBlocks.ContainsValue(filterModel.BlockHash))
				{
					await ProcessFilterModelAsync(filterModel, CancellationToken.None);
				}
			}
			NewFilterProcessed?.Invoke(this, filterModel);
		}

		public async Task InitializeAsync(CancellationToken cancel)
		{
			if (!IndexDownloader.IsRunning)
			{
				throw new NotSupportedException($"{nameof(IndexDownloader)} is not running.");
			}

			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				// Go through the filters and que to download the matches.
				var filters = IndexDownloader.GetFiltersIncluding(IndexDownloader.StartingFilter.BlockHeight);

				foreach (FilterModel filterModel in filters.Where(x => !(x.Filter is null) && !WalletBlocks.ContainsValue(x.BlockHash))) // Filter can be null if there is no bech32 tx.
				{
					await ProcessFilterModelAsync(filterModel, cancel);
				}

				// Load in dummy mempool
				if (File.Exists(TransactionsFilePath))
				{
					string jsonString = File.ReadAllText(TransactionsFilePath, Encoding.UTF8);
					var serializedTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString);

					foreach (SmartTransaction tx in serializedTransactions.Where(x => !x.Confirmed))
					{
						try
						{
							await SendTransactionAsync(tx);

							ProcessTransaction(tx);
						}
						catch (Exception ex)
						{
							Logger.LogWarning<WalletService>(ex);
						}
					}
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
			if (!(dontTouch is null))
			{
				keys = keys.Except(dontTouch);
				if (!keys.Any())
				{
					throw new InvalidOperationException($"{nameof(dontTouch)} covers all the possible keys.");
				}
			}

			var foundLabelless = keys.FirstOrDefault(x => !x.HasLabel()); // return the first labelless
			HdPubKey ret = foundLabelless ?? keys.RandomElement(); // return the first, because that's the oldest

			ret.SetLabel(label, KeyManager);

			return ret;
		}

		public List<SmartCoin> GetHistory(SmartCoin coin, IEnumerable<SmartCoin> current)
		{
			Guard.NotNull(nameof(coin), coin);
			if (current.Contains(coin))
			{
				return current.ToList();
			}
			var history = current.Concat(new List<SmartCoin> { coin }).ToList(); // the coin is the firs elem in its history

			// If the script is the same then we have a match, no matter of the anonimity set.
			foreach (var c in Coins)
			{
				if (c.ScriptPubKey == coin.ScriptPubKey)
				{
					if (!history.Contains(c))
					{
						var h = GetHistory(c, history);
						foreach (var hr in h)
						{
							if (!history.Contains(hr))
							{
								history.Add(hr);
							}
						}
					}
				}
			}

			// If it spends someone and haven't been sufficiently anonimized.
			if (coin.AnonymitySet < 50)
			{
				var c = Coins.FirstOrDefault(x => x.SpenderTransactionId == coin.TransactionId && !history.Contains(x));
				if (c != default)
				{
					var h = GetHistory(c, history);
					foreach (var hr in h)
					{
						if (!history.Contains(hr))
						{
							history.Add(hr);
						}
					}
				}
			}

			// If it's being spent by someone and that someone haven't been sufficiently anonimized.
			if (!coin.Unspent)
			{
				var c = Coins.FirstOrDefault(x => x.TransactionId == coin.SpenderTransactionId && !history.Contains(x));
				if (c != default)
				{
					if (c.AnonymitySet < 50)
					{
						if (c != default)
						{
							var h = GetHistory(c, history);
							foreach (var hr in h)
							{
								if (!history.Contains(hr))
								{
									history.Add(hr);
								}
							}
						}
					}
				}
			}

			return history;
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

			if (tx.Height.Type == HeightType.Chain)
			{
				MemPool.TransactionHashes.TryRemove(txId); // If we have in mempool, remove.
				if (!tx.Transaction.SpendsOrReceivesWitness()) return; // We don't care about non-witness transactions for other than mempool cleanup.

				bool isFoundTx = TransactionCache.Contains(tx); // If we have in cache, update height.
				if (isFoundTx)
				{
					SmartTransaction foundTx = TransactionCache.FirstOrDefault(x => x == tx);
					if (foundTx != default(SmartTransaction)) // Must check again, because it's a concurrent collection!
					{
						foundTx.SetHeight(tx.Height);
					}
				}
			}
			else
			{
				if (!tx.Transaction.SpendsOrReceivesWitness()) 
					return; // We don't care about non-witness transactions for other than mempool cleanup.
			}

			//iterate tx
			//	if already have the coin
			//		if NOT mempool
			//			update height

			//if double spend
			//	if mempool
			//		if all double spent coins are mempool and RBF
			//			remove double spent coins(if other coin spends it, remove that too and so on) // will add later if they came to our keys
			//		else
			//			return
			//	else // new confirmation always enjoys priority
			//		remove double spent coins recursively(if other coin spends it, remove that too and so on)// will add later if they came to our keys

			//iterate tx
			//	if came to our keys
			//		add coin

			for (var i = 0; i < tx.Transaction.Outputs.Count; i++)
			{
				// If we already had it, just update the height. Maybe got from mempool to block or reorged.
				SmartCoin foundCoin = Coins.FirstOrDefault(x => x.TransactionId == txId && x.Index == i);
				if (foundCoin != default)
				{
					// If tx height is mempool then don't, otherwise update the height.
					if (tx.Height != Height.MemPool)
					{
						foundCoin.Height = tx.Height;
					}
				}
			}

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
						// remove double spent coins(if other coin spends it, remove that too and so on) // will add later if they came to our keys
						foreach (var doubleSpentCoin in doubleSpends)
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
					foreach (var doubleSpentCoin in doubleSpends)
					{
						RemoveCoinRecursively(doubleSpentCoin);
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
					var mixin = tx.Transaction.GetMixin(i);
					if (spentOwnCoins.Count != 0)
					{
						mixin += spentOwnCoins.Min(x => x.Mixin);
					}
					var coin = new SmartCoin(txId, i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToTxoRefs().ToArray(), tx.Height, tx.Transaction.RBF, mixin, foundKey.Label, spenderTransactionId: null, false); // Don't inherit locked status from key, that's different.
					ChaumianClient.State.UpdateCoin(coin);
					Coins.TryAdd(coin);
					TransactionCache.Add(tx);
					CoinReceived?.Invoke(this, coin);
					if (coin.Unspent && !(ChaumianClient.OnePiece is null) && coin.Label == "ZeroLink Change")
					{
						Task.Run(async () =>
						{
							try
							{
								await ChaumianClient.QueueCoinsToMixAsync(ChaumianClient.OnePiece, coin);
							}
							catch (Exception ex)
							{
								Logger.LogError<WalletService>(ex);
							}
						});
					}

					// Make sure there's always 21 clean keys generated and indexed.
					KeyManager.AssertCleanKeysIndexed(21, foundKey.IsInternal());

					if (foundKey.IsInternal())
					{
						// Make sure there's always 14 internal locked keys generated and indexed.
						KeyManager.AssertLockedInternalKeysIndexed(14);
					}
				}
			}

			// If spends any of our coin
			for (var i = 0; i < tx.Transaction.Inputs.Count; i++)
			{
				var input = tx.Transaction.Inputs[i];

				var foundCoin = Coins.FirstOrDefault(x => x.TransactionId == input.PrevOut.Hash && x.Index == input.PrevOut.N);
				if (!(foundCoin is null))
				{
					foundCoin.SpenderTransactionId = txId;
					TransactionCache.Add(tx);
					CoinSpentOrSpenderConfirmed?.Invoke(this, foundCoin);
				}
			}
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
						return Block.Load(blockBytes, IndexDownloader.Network);
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

						if (!node.IsConnected && !(IndexDownloader.Network != Network.RegTest))
						{
							await Task.Delay(100);
							continue;
						}

						try
						{
							Interlocked.Increment(ref _concurrentBlockDownload);
							ConcurrentBlockDownloadNumberChanged?.Invoke(this, _concurrentBlockDownload);

							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(32))) // ADSL	512 kbit/s	00:00:32
							{
								block = node.GetBlocks(new uint256[] { hash }, cts.Token)?.Single();
							}

							if (block is null)
							{
								Logger.LogInfo<WalletService>("Disconnected node, because couldn't parse received block.");
								node.DisconnectAsync("Couldn't parse block.");
								continue;
							}

							if (!block.Check())
							{
								Logger.LogInfo<WalletService>("Disconnected node, because block invalid block received.");
								node.DisconnectAsync("Invalid block received.");
								continue;
							}
						}
						catch (TimeoutException)
						{
							Logger.LogInfo<WalletService>("Disconnected node, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (OperationCanceledException)
						{
							Logger.LogInfo<WalletService>("Disconnected node, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (Exception ex)
						{
							Logger.LogDebug<WalletService>(ex);
							Logger.LogInfo<WalletService>($"Disconnected node, because block download failed: {ex.Message}");
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
		public async Task<BuildTransactionResult> BuildTransactionAsync(string password,
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
			if (!(subtractFeeFromAmountIndex is null)) // If not null, make sure not out of range. If null fee is substracted from the change.
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
			if (!(allowedInputs is null)) // If allowedInputs are specified then select the coins from them.
			{
				if (!allowedInputs.Any())
				{
					throw new ArgumentException($"{nameof(allowedInputs)} is not null, but empty.");
				}

				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrCoinJoinInProgress && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrCoinJoinInProgress && x.Confirmed && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();
				}
			}
			else
			{
				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrCoinJoinInProgress).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrCoinJoinInProgress && x.Confirmed).ToList();
				}
			}

			// 4. Get and calculate fee
			Logger.LogInfo<WalletService>("Calculating dynamic transaction fee...");

			Money feePerBytes = null;
			using (var client = new WasabiClient(IndexDownloader.WasabiClient.TorClient.DestinationUri, IndexDownloader.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				var fees = await client.GetFeesAsync(feeTarget);
				Money feeRate = fees.Single().Value.Conservative;
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
			var origTxSize = inNum * Constants.P2pkhInputSizeInBytes + outNum * Constants.OutputSizeInBytes + 10;
			var newTxSize = inNum * Constants.P2wpkhInputSizeInBytes + outNum * Constants.OutputSizeInBytes + 10; // BEWARE: This assumes segwit only inputs!
			var vSize = (int)Math.Ceiling(((3 * newTxSize) + origTxSize) / 4m);
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
					throw new InsufficientBalanceException(fee + 1, realToSend[i].amount + fee);
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
			var changeLabel = $"change of ({sb.ToString().TrimEnd(',', ' ')})";

			if (customChange is null)
			{
				KeyManager.AssertCleanKeysIndexed(21, true);
				KeyManager.AssertLockedInternalKeysIndexed(14);
				var changeHdPubKey = KeyManager.GetKeys(KeyState.Clean, true).RandomElement();

				changeHdPubKey.SetLabel(changeLabel, KeyManager);
				changeScriptPubKey = changeHdPubKey.GetP2wpkhScript();
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
			var builder = new TransactionBuilder();
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
				.Shuffle()
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
				var mixin = tx.GetMixin(i) + spentCoins.Min(x => x.Mixin);
				var coin = new SmartCoin(tx.GetHash(), i, output.ScriptPubKey, output.Value, tx.Inputs.ToTxoRefs().ToArray(), Height.Unknown, tx.RBF, mixin);
				if (KeyManager.GetKeys(KeyState.Clean).Select(x => x.GetP2wpkhScript()).Contains(coin.ScriptPubKey))
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
			foreach (var coin in unspentCoins)
				if (coin.Confirmed) unspentConfirmedCoins.Add(coin);
				else unspentUnconfirmedCoins.Add(coin);

			bool haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentConfirmedCoins);
			if (!haveEnough)
				haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentUnconfirmedCoins);
			if (!haveEnough)
				throw new InsufficientBalanceException(totalOutAmount, unspentConfirmedCoins.Select(x => x.Amount).Sum() + unspentUnconfirmedCoins.Select(x => x.Amount).Sum());

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

		public void Renamelabel(SmartCoin coin, string newLabel)
		{
			newLabel = Guard.Correct(newLabel);
			coin.Label = newLabel;
			var key = KeyManager.GetKeys().SingleOrDefault(x => x.GetP2wpkhScript() == coin.ScriptPubKey);
			if (!(key is null))
			{
				key.SetLabel(newLabel, KeyManager);
			}
		}

		public async Task SendTransactionAsync(SmartTransaction transaction)
		{
			using (var client = new WasabiClient(IndexDownloader.WasabiClient.TorClient.DestinationUri, IndexDownloader.WasabiClient.TorClient.TorSocks5EndPoint))
			{
				await client.BroadcastAsync(transaction);
			}

			ProcessTransaction(new SmartTransaction(transaction.Transaction, Height.MemPool));
			MemPool.TransactionHashes.Add(transaction.GetHash());

			Logger.LogInfo<WalletService>($"Transaction is successfully broadcasted: {transaction.GetHash()}.");
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					IndexDownloader.NewFilter -= IndexDownloader_NewFilterAsync;
					IndexDownloader.Reorged -= IndexDownloader_ReorgedAsync;
					MemPool.TransactionReceived -= MemPool_TransactionReceived;

					IoHelpers.EnsureContainingDirectoryExists(TransactionsFilePath);
					string jsonString = JsonConvert.SerializeObject(TransactionCache, Formatting.Indented);
					File.WriteAllText(TransactionsFilePath,
						jsonString,
						Encoding.UTF8);
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
