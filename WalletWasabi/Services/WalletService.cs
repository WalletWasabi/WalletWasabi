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
using WalletWasabi.Backend.Models;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.TorSocks5;
using NBitcoin;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using Nito.AsyncEx;

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

		private AsyncLock HandleFiltersLock { get; }
		private AsyncLock BlockDownloadLock { get; }
		private AsyncLock BlockFolderLock { get; }

		public SortedDictionary<Height, uint256> WalletBlocks { get; }
		private HashSet<uint256> ProcessedBlocks { get; }
		private AsyncLock WalletBlocksLock { get; }

		public ConcurrentHashSet<SmartCoin> Coins { get; }

		public event EventHandler<FilterModel> NewFilterProcessed;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public WalletService(KeyManager keyManager, IndexDownloader indexDownloader, CcjClient chaumianClient, MemPoolService memPool, NodesGroup nodes, string blocksFolderPath)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			IndexDownloader = Guard.NotNull(nameof(indexDownloader), indexDownloader);
			ChaumianClient = Guard.NotNull(nameof(chaumianClient), chaumianClient);
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
					foreach (var toRemove in Coins.Where(x => x.Height == elem.Key).ToHashSet())
					{
						RemoveCoinRecursively(toRemove);
					}
				}
			}
		}

		private void RemoveCoinRecursively(SmartCoin toRemove)
		{
			if (toRemove.SpenderTransactionId != null)
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
				if (filterModel.Filter != null && !WalletBlocks.ContainsValue(filterModel.BlockHash))
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
				foreach (var blockRef in WalletBlocks)
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
			label = Guard.Correct(label);

			// Make sure there's always 21 clean keys generated and indexed.
			AssertCleanKeysIndexed(21, false);

			var ret = KeyManager.GetKeys(KeyState.Clean, false).RandomElement();

			ret.SetLabel(label, KeyManager);

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
					KeyManager.GenerateNewKey("", KeyState.Clean, true, toFile: false);
					generated = true;
				}
				while (KeyManager.GetKeys(KeyState.Clean, false).Count() < howMany)
				{
					KeyManager.GenerateNewKey("", KeyState.Clean, false, toFile: false);
					generated = true;
				}
			}
			else
			{
				while (KeyManager.GetKeys(KeyState.Clean, isInternal).Count() < howMany)
				{
					KeyManager.GenerateNewKey("", KeyState.Clean, (bool)isInternal, toFile: false);
					generated = true;
				}
			}

			if (generated)
			{
				KeyManager.ToFile();
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

			// If key list is not provided refresh the key list.
			if (keys == null)
			{
				keys = KeyManager.GetKeys().ToList();
			}

			for (var i = 0; i < tx.Transaction.Outputs.Count; i++)
			{
				// If we already had it, just update the height. Maybe got from mempool to block or reorged.
				var foundCoin = Coins.SingleOrDefault(x => x.TransactionId == tx.GetHash() && x.Index == i);
				if (foundCoin != default)
				{
					// If tx height is mempool then don't, otherwise update the height.
					if (tx.Height != Height.MemPool)
					{
						foundCoin.Height = tx.Height;
					}
				}
			}

			// If double spend:
			IEnumerable<SmartCoin> doubleSpends = Coins.Where(x => tx.Transaction.Inputs.Any(y => x.SpentOutputs.Select(z => z.ToOutPoint()).Contains(y.PrevOut)));
			if (doubleSpends.Count() > 0)
			{
				if (tx.Height == Height.MemPool)
				{
					// if all double spent coins are mempool and RBF
					if (doubleSpends.All(x => x.Height == Height.MemPool && x.RBF))
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
				HdPubKey foundKey = keys.SingleOrDefault(x => x.GetP2wpkhScript() == output.ScriptPubKey);
				if (foundKey != default)
				{
					foundKey.SetKeyState(KeyState.Used, KeyManager);
					List<SmartCoin> spentOwnCoins = Coins.Where(x => tx.Transaction.Inputs.Any(y => y.PrevOut.Hash == x.TransactionId && y.PrevOut.N == x.Index)).ToList();
					var mixin = tx.Transaction.GetMixin((uint)i);
					if (spentOwnCoins.Count != 0)
					{
						mixin += spentOwnCoins.Min(x => x.Mixin);
					}
					var coin = new SmartCoin(tx.GetHash(), i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToTxoRefs().ToArray(), tx.Height, tx.Transaction.RBF, mixin, foundKey.Label, spenderTransactionId: null, locked: false); // Don't inherit locked status from key, that's different.
					Coins.Add(coin);
					if (coin.Unspent && coin.Label == "ZeroLink Change" && ChaumianClient.OnePiece != null)
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
					if (AssertCleanKeysIndexed(21, foundKey.IsInternal()))
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
		public async Task<BuildTransactionResult> BuildTransactionAsync(string password, Operation[] toSend, int feeTarget, bool allowUnconfirmed = false, int? subtractFeeFromAmountIndex = null, Script customChange = null, IEnumerable<TxoRef> allowedInputs = null)
		{
			password = password ?? ""; // Correction.
			toSend = Guard.NotNullOrEmpty(nameof(toSend), toSend);
			if (toSend.Any(x => x == null))
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
			if (spendAllCount == 1 && customChange != null)
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
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrLocked && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrLocked && x.Confirmed && allowedInputs.Any(y => y.TransactionId == x.TransactionId && y.Index == x.Index)).ToList();
				}
			}
			else
			{
				if (allowUnconfirmed)
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrLocked).ToList();
				}
				else
				{
					allowedSmartCoinInputs = Coins.Where(x => !x.SpentOrLocked && x.Confirmed).ToList();
				}
			}

			// 4. Get and calculate fee
			Logger.LogInfo<WalletService>("Calculating dynamic transaction fee...");
			Money feePerBytes = null;
			using (var torClient = new TorHttpClient(IndexDownloader.Client.DestinationUri, IndexDownloader.Client.TorSocks5EndPoint, isolateStream: true))
			using (var response = await torClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v1/btc/blockchain/fees/{feeTarget}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
					throw new HttpRequestException($"Couldn't query network fees. Reason: {response.StatusCode.ToReasonString()}");

				using (var content = response.Content)
				{
					var json = await content.ReadAsJsonAsync<SortedDictionary<int, FeeEstimationPair>>();
					feePerBytes = new Money(json.Single().Value.Conservative);
				}
			}

			bool spendAll = spendAllCount == 1;
			int inNum;
			if (spendAll)
			{
				inNum = allowedSmartCoinInputs.Count();
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
			Logger.LogInfo<WalletService>($"Estimated tx size: {vSize} bytes.");
			Money fee = feePerBytes * vSize;
			Logger.LogInfo<WalletService>($"Fee: {fee.ToString(fplus: false, trimExcessZero: true)}");

			// 5. How much to spend?
			long toSendAmountSumInSatoshis = toSend.Select(x => x.Amount).Sum(); // Does it work if I simply go with Money class here? Is that copied by reference of value?
			var realToSend = new(Script script, Money amount, string label)[toSend.Length];
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

					if (subtractFeeFromAmountIndex == null)
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
					throw new InsufficientBalanceException(Money.Zero, realToSend[i].amount);
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

			if (customChange == null)
			{
				AssertCleanKeysIndexed(21, true);
				var changeHdPubKey = KeyManager.GetKeys(KeyState.Clean, true).RandomElement();

				changeHdPubKey.SetLabel(changeLabel, KeyManager);
				changeScriptPubKey = changeHdPubKey.GetP2wpkhScript();
			}
			else
			{
				changeScriptPubKey = customChange;
			}

			// 6. Do some checks
			Money totalOutgoingAmount = realToSend.Select(x => x.amount).Sum() + fee;
			decimal feePc = (100 * fee.ToDecimal(MoneyUnit.BTC)) / totalOutgoingAmount.ToDecimal(MoneyUnit.BTC);

			if (feePc > 1)
			{
				Logger.LogInfo<WalletService>($"The transaction fee is {feePc:0.#}% of your transaction amount."
					+ Environment.NewLine + $"Sending:\t {totalOutgoingAmount.ToString(fplus: false, trimExcessZero: true)} BTC."
					+ Environment.NewLine + $"Fee:\t\t {fee.ToString(fplus: false, trimExcessZero: true)} BTC.");
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
			if (key != null)
			{
				key.SetLabel(newLabel, KeyManager);
			}
		}

		public async Task SendTransactionAsync(SmartTransaction transaction)
		{
			using (var torClient = new TorHttpClient(IndexDownloader.Client.DestinationUri, IndexDownloader.Client.TorSocks5EndPoint, isolateStream: true))
			using (var content = new StringContent($"'{transaction.Transaction.ToHex()}'", Encoding.UTF8, "application/json"))
			using (var response = await torClient.SendAsync(HttpMethod.Post, "/api/v1/btc/blockchain/broadcast", content))
			{
				if (response.StatusCode == HttpStatusCode.BadRequest)
				{
					throw new HttpRequestException($"Couldn't broadcast transaction. Reason: {await response.Content.ReadAsStringAsync()}");
				}
				if (response.StatusCode != HttpStatusCode.OK) // Try again.
				{
					throw new HttpRequestException($"Couldn't broadcast transaction. Reason: {response.StatusCode.ToReasonString()}");
				}
			}

			ProcessTransaction(new SmartTransaction(transaction.Transaction, Height.MemPool));

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
