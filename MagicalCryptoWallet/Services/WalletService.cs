using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.TorSocks5;
using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;

namespace MagicalCryptoWallet.Services
{
	public class WalletService : IDisposable
	{
		public KeyManager KeyManager { get; }
		public IndexDownloader IndexDownloader { get; }
		public MemPoolService MemPool { get; }

		public NodesGroup Nodes { get; }
		public string BlocksFolderPath { get; }

		private AsyncLock HandleFiltersLock { get; }
		private AsyncLock BlockDownloadLock { get; }
		private AsyncLock BlockFolderLock { get; }

		public SortedDictionary<Height, uint256> WalletBlocks { get; }
		private HashSet<uint256> ProcessedBlocks { get; }
		private AsyncLock WalletBlocksLock { get; }

		public ConcurrentHashSet<SmartCoin> Coins { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;
		private void OnNewFilterProcessed(FilterModel filter) => NewFilterProcessed?.Invoke(this, filter);

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public WalletService(KeyManager keyManager, IndexDownloader indexDownloader, MemPoolService memPool, NodesGroup nodes, string blocksFolderPath)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			IndexDownloader = Guard.NotNull(nameof(indexDownloader), indexDownloader);
			MemPool = Guard.NotNull(nameof(memPool), memPool);

			WalletBlocks = new SortedDictionary<Height, uint256>();
			ProcessedBlocks = new HashSet<uint256>();
			WalletBlocksLock = new AsyncLock();
			HandleFiltersLock = new AsyncLock();

			Coins = new ConcurrentHashSet<SmartCoin>();

			BlocksFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(blocksFolderPath), blocksFolderPath, trim: true);
			BlockFolderLock = new AsyncLock();
			BlockDownloadLock = new AsyncLock();

			AssertCleanKeysIndexed(21);

			_running = 0;

			if (Directory.Exists(BlocksFolderPath))
			{
				if(IndexDownloader.Network == Network.RegTest)
				{
					Directory.Delete(BlocksFolderPath, true);
					Directory.CreateDirectory(BlocksFolderPath);
				}
			}
			else
			{
				Directory.CreateDirectory(BlocksFolderPath);
			}

			IndexDownloader.NewFilter += IndexDownloader_NewFilterAsync;
			IndexDownloader.Reorged += IndexDownloader_ReorgedAsync;
			MemPool.TransactionReceived += MemPool_TransactionReceived;
		}

		private void MemPool_TransactionReceived(object sender, SmartTransaction tx)
		{
			ProcessTransaction(tx, keys: null);
		}

		private async void IndexDownloader_ReorgedAsync(object sender, uint256 invalidBlockHash)
		{
			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				var elem = WalletBlocks.SingleOrDefault(x => x.Value == invalidBlockHash);
				await DeleteBlockAsync(invalidBlockHash);
				WalletBlocks.RemoveByValue(invalidBlockHash);
				ProcessedBlocks.Remove(invalidBlockHash);
				if (elem.Key != null)
				{
					foreach(var toRemove in Coins.Where(x => x.Height == elem.Key).ToHashSet())
					{
						RemoveCoinRecursively(toRemove);
					}
				}
			}
		}

		private void RemoveCoinRecursively(SmartCoin toRemove)
		{
			if(toRemove.SpenderTransactionId != null)
			{
				foreach(var toAlsoRemove in Coins.Where(x=>x.TransactionId == toRemove.SpenderTransactionId).ToHashSet())
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
				if (filterModel.Filter != null && !WalletBlocks.ContainsValue(filterModel.BlockHash))
				{
					await ProcessFilterModelAsync(filterModel, CancellationToken.None);
				}
			}
			OnNewFilterProcessed(filterModel);
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

				foreach (var filterModel in filters.Where(x => x.Filter != null && !WalletBlocks.ContainsValue(x.BlockHash))) // Filter can be null if there is no bech32 tx.
				{
					await ProcessFilterModelAsync(filterModel, cancel);
				}
			}
		}

		private async Task ProcessFilterModelAsync(FilterModel filterModel, CancellationToken cancel)
		{
			if (ProcessedBlocks.Contains(filterModel.BlockHash))
			{
				return;
			}

			var matchFound = filterModel.Filter.MatchAny(KeyManager.GetKeys().Select(x => x.GetP2wpkhScript().ToCompressedBytes()), filterModel.FilterKey);
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
				foreach(var blockRef in WalletBlocks)
				{
					var block = await GetOrDownloadBlockAsync(blockRef.Value, CancellationToken.None);
					ProcessedBlocks.Clear();
					Coins.Clear();
					ProcessBlock(blockRef.Key, block);
				}
			}
		}

		public HdPubKey GetReceiveKey(string label)
		{
			// ToDo, put this correction pattern into the Guard class: Guard.Correct(string ...)
			if (string.IsNullOrWhiteSpace(label))
			{
				label = "";
			}
			else
			{
				label = label.Trim();
			}

			// Make sure there's always 21 clean keys generated and indexed.
			AssertCleanKeysIndexed(21, false);

			var ret = KeyManager.GetKeys(KeyState.Clean, false).RandomElement();

			ret.Label = label;

			return ret;
		}

		/// <summary>
		/// Make sure there's always clean keys generated and indexed.
		/// </summary>
		private bool AssertCleanKeysIndexed(int howMany = 21, bool? isInternal = null)
		{
			var generated = false;

			if (isInternal == null)
			{
				while (KeyManager.GetKeys(KeyState.Clean, true).Count() < howMany)
				{
					KeyManager.GenerateNewKey("", KeyState.Clean, true);
					generated = true;
				}
				while (KeyManager.GetKeys(KeyState.Clean, false).Count() < howMany)
				{
					KeyManager.GenerateNewKey("", KeyState.Clean, false);
					generated = true;
				}
			}
			else
			{
				while (KeyManager.GetKeys(KeyState.Clean, isInternal).Count() < howMany)
				{
					KeyManager.GenerateNewKey("", KeyState.Clean, (bool)isInternal);
					generated = true;
				}
			}
			return generated;
		}

		private void ProcessBlock(Height height, Block block)
		{
			var keys = KeyManager.GetKeys().ToList();

			foreach (var tx in block.Transactions)
			{
				ProcessTransaction(new SmartTransaction(tx, height), keys);
			}

			ProcessedBlocks.Add(block.GetHash());
		}

		private void ProcessTransaction(SmartTransaction tx, List<HdPubKey> keys = null)
		{
			// If key list is not provided refresh the key list.
			if(keys == null)
			{
				keys = KeyManager.GetKeys().ToList();
			}

			// If transaction received to any of the wallet keys:
			for (var i = 0; i < tx.Transaction.Outputs.Count; i++)
			{
				var output = tx.Transaction.Outputs[i];
				HdPubKey foundKey = keys.SingleOrDefault(x => x.GetP2wpkhScript() == output.ScriptPubKey);
				if (foundKey != default)
				{
					// If we already had it, just update the height. Maybe got from mempool to block or reorged.
					var foundCoin = Coins.SingleOrDefault(x => x.TransactionId == tx.GetHash() && x.Index == i);
					if(foundCoin != default)
					{
						// If tx height is mempool then don't, otherwise update the height.
						if (tx.Height == Height.MemPool)
						{
							continue;
						}
						else
						{
							foundCoin.Height = tx.Height;
							continue;
						}
					}

					foundKey.KeyState = KeyState.Used;
					var coin = new SmartCoin(tx.GetHash(), i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToTxoRefs().ToArray(), tx.Height, foundKey.Label, null);
					Coins.Add(coin);

					// Make sure there's always 21 clean keys generated and indexed.
					if(AssertCleanKeysIndexed(21, foundKey.IsInternal()))
					{
						// If it generated a new key refresh the keys:
						keys = KeyManager.GetKeys().ToList();
					}
				}
			}

			// If spends any of our coin
			for (var i = 0; i < tx.Transaction.Inputs.Count; i++)
			{
				var input = tx.Transaction.Inputs[i];

				var foundCoin = Coins.SingleOrDefault(x => x.TransactionId == input.PrevOut.Hash && x.Index == input.PrevOut.N);
				if (foundCoin != null)
				{
					foundCoin.SpenderTransactionId = tx.GetHash();
				}
			}			
		}

		/// <exception cref="OperationCanceledException"></exception>
		public async Task<Block> GetOrDownloadBlockAsync(uint256 hash, CancellationToken cancel)
		{
			// Try get the block
			using (await BlockFolderLock.LockAsync())
			{
				foreach (var filePath in Directory.EnumerateFiles(BlocksFolderPath))
				{
					var fileName = Path.GetFileName(filePath);
					if (hash == new uint256(fileName))
					{
						var blockBytes = await File.ReadAllBytesAsync(filePath);
						return new Block(blockBytes);
					}
				}
			}
			cancel.ThrowIfCancellationRequested();

			// Download the block
			Block block = null;
			using (await BlockDownloadLock.LockAsync())
			{
				while(true)
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
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(32))) // ADSL	512 kbit/s	00:00:32
							{
								block = node.GetBlocks(new uint256[] { hash }, cts.Token)?.Single();
							}

							if (block == null)
							{
								Logger.LogInfo<WalletService>($"Disconnected node, because couldn't parse received block.");
								node.DisconnectAsync("Couldn't parse block.");
								continue;
							}

							if (!block.Check())
							{
								Logger.LogInfo<WalletService>($"Disconnected node, because block invalid block received.");
								node.DisconnectAsync("Invalid block received.");
								continue;
							}
						}
						catch (TimeoutException)
						{
							Logger.LogInfo<WalletService>($"Disconnected node, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (OperationCanceledException)
						{
							Logger.LogInfo<WalletService>($"Disconnected node, because block download took too long.");
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

		public async Task<int> CountBlocksAsync()
		{
			using (await BlockFolderLock.LockAsync())
			{
				return Directory.EnumerateFiles(BlocksFolderPath).Count();
			}
		}

		/// <param name="toSend">If Money.Zero then spend all available amount. Don't generate change.</param>
		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		/// <param name="allowedInputs">Only these inputs allowed to be used to build the transaction. The wallet must know the corresponding private keys.</param>
		/// <param name="subtractFeeFromAmountIndex">If null, fee is substracted from the change. Otherwise it denotes the index in the toSend array.</param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public async Task BuildTransactionAsync(string password, IEnumerable<(Script script, Money money)> toSend, int feeTarget, bool allowUnconfirmed = false, int? subtractFeeFromAmountIndex = null, Script customChange = null, IEnumerable<TxoRef> allowedInputs = null)
		{
			password = password ?? ""; // Correction.
			toSend = Guard.NotNullOrEmpty(nameof(toSend), toSend);
			int sendAllCount = toSend.Count(x => x.money == Money.Zero);
			if (sendAllCount > 1)
			{
				throw new ArgumentException($"Only one {nameof(toSend)} element can contain Money.Zero. Money.Zero means add the change to the value of this output.");
			}
			if (sendAllCount == 1 && customChange != null)
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
				if (subtractFeeFromAmountIndex > toSend.Count() - 1)
				{
					throw new ArgumentOutOfRangeException($"{nameof(subtractFeeFromAmountIndex)} can be maximum {nameof(toSend)}.Count() - 1. {nameof(subtractFeeFromAmountIndex)}: {subtractFeeFromAmountIndex}, {nameof(toSend)}.Count() - 1: {toSend.Count() - 1}.");
				}
			}

			// Get allowed coins to spend.
			List<SmartCoin> allowedSmartCoinInputs; // Inputs those can be used to build the transaction.
			if (allowedInputs != null && allowedInputs.Count() != 0) // If allowedInputs are specified then select the coins from them.
			{
				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.Where(x => allowedInputs.Count(y => y.TransactionId == x.TransactionId && y.Index == x.Index) > 0).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => x.Height.IsConfirmed && allowedInputs.Count(y => y.TransactionId == x.TransactionId && y.Index == x.Index) > 0).ToList();
				}
			}
			else
			{
				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => x.Height.IsConfirmed).ToList();
				}
			}

			// 1. Get the possible changes.
			List<Script> allowedChanges;
			if (customChange == null)
			{
				AssertCleanKeysIndexed(21, true);
				allowedChanges = KeyManager.GetKeys(KeyState.Clean, true).Select(x => x.GetP2wpkhScript()).ToList();
			}
			else
			{
				allowedChanges = new List<Script> { customChange };
			}

			// 2. Find all coins I can spend from the account
			// 3. How much money we can spend?
			Money spendableConfirmedAmount = Coins.Where(x => x.Height.IsConfirmed).Sum(x => x.Amount);
			Logger.LogInfo<WalletService>($"Spendable confirmed amount: {spendableConfirmedAmount}.");
			Money spendableUnconfirmedAmount;
			if (allowUnconfirmed)
			{
				spendableUnconfirmedAmount = Coins.Sum(x => x.Amount);
				Logger.LogInfo<WalletService>($"Spendable unconfirmed amount: {spendableUnconfirmedAmount}.");
			}
			else
			{
				spendableUnconfirmedAmount = Money.Zero;
			}

			// 4. Get and calculate fee
			Logger.LogInfo<WalletService>("Calculating dynamic transaction fee...");
			Money feePerBytes = null;
			using (var torClient = new TorHttpClient(IndexDownloader.Client.DestinationUri, IndexDownloader.Client.TorSocks5EndPoint, isolateStream: true))
			using (var response = await torClient.SendAsync(HttpMethod.Get, $"/api/v1/btc/blockchain/fees/{feeTarget}"))
			{
				if (response.StatusCode != HttpStatusCode.OK) // Try again.
				{
					using (var response2 = await torClient.SendAsync(HttpMethod.Get, $"/api/v1/btc/blockchain/fees/{feeTarget}"))
					{
						await Task.Delay(1000);
						if (response2.StatusCode != HttpStatusCode.OK)
						{
							throw new HttpRequestException($"Couldn't query network fees. Reason: {response2.StatusCode.ToReasonString()}");
						}
						else
						{
							using (var content = response.Content)
							{
								var json = await content.ReadAsJsonAsync<SortedDictionary<int, FeeEstimationPair>>();
								feePerBytes = new Money(json.Single().Value.Conservative);
							}
						}
					}
				}
				else
				{
					using (var content = response.Content)
					{
						var json = await content.ReadAsJsonAsync<SortedDictionary<int, FeeEstimationPair>>();
						feePerBytes = new Money(json.Single().Value.Conservative);
					}
				}
			}
			
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

		#endregion
	}
}
