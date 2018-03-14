using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;

namespace MagicalCryptoWallet.Services
{
	public class WalletService
	{
		public KeyManager KeyManager { get; }
		public BlockDownloader BlockDownloader { get; }
		public IndexDownloader IndexDownloader { get; }

		public NodesGroup Nodes { get; }
		public string BlocksFolderPath { get; }
		private AsyncLock BlocksFolderLock { get; }

		private AsyncLock HandleFiltersLock { get; }

		public SortedDictionary<Height, uint256> WalletBlocks { get; }
		private HashSet<uint256> ProcessedBlocks { get; }
		private AsyncLock WalletBlocksLock { get; }

		public ConcurrentHashSet<SmartCoin> KnownCoins { get; }

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public WalletService(KeyManager keyManager, IndexDownloader indexDownloader, NodesGroup nodes, string blocksFolderPath)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			IndexDownloader = Guard.NotNull(nameof(indexDownloader), indexDownloader);

			WalletBlocks = new SortedDictionary<Height, uint256>();
			ProcessedBlocks = new HashSet<uint256>();
			WalletBlocksLock = new AsyncLock();
			HandleFiltersLock = new AsyncLock();

			KnownCoins = new ConcurrentHashSet<SmartCoin>();

			BlocksFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(blocksFolderPath), blocksFolderPath, trim: true);
			BlocksFolderLock = new AsyncLock();

			_running = 0;

			if (Directory.Exists(BlocksFolderPath))
			{
				foreach (var blockFilePath in Directory.EnumerateFiles(BlocksFolderPath))
				{
					var blockBytes = File.ReadAllBytes(blockFilePath);
					var block = new Block(blockBytes);
				}
			}
			else
			{
				Directory.CreateDirectory(BlocksFolderPath);
			}

			IndexDownloader.NewFilter += IndexDownloader_NewFilter;
			IndexDownloader.Reorged += IndexDownloader_Reorged;
			BlockDownloader.NewBlock += BlockDownloader_NewBlock;
		}

		private void BlockDownloader_NewBlock(object sender, Block block)
		{
			using (WalletBlocksLock.Lock())
			{
				//Todo!

				if(ProcessedBlocks.Contains(block.GetHash()))
				{
					return;
				}

				if(block.GetHash() == WalletBlocks.Last().Value) // If this is the latest block then no need to look through everything.
				{

					ProcessedBlocks.Add(block.GetHash());
				}
				else // Do a deep reindexing.
				{
					ProcessedBlocks.Clear();
				}
			}
		}

		private void IndexDownloader_Reorged(object sender, uint256 invalidBlockHash)
		{
			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				var elem = WalletBlocks.SingleOrDefault(x => x.Value == invalidBlockHash);
				BlockDownloader.TryRemove(invalidBlockHash);
				WalletBlocks.RemoveByValue(invalidBlockHash);
				ProcessedBlocks.Remove(invalidBlockHash);
				if (elem.Key != null)
				{
					foreach(var toRemove in KnownCoins.Where(x => x.Height == elem.Key).ToHashSet())
					{
						KnownCoins.TryRemove(toRemove);
					}
				}
			}
		}

		private void IndexDownloader_NewFilter(object sender, FilterModel filterModel)
		{
			using (HandleFiltersLock.Lock())
			using (WalletBlocksLock.Lock())
			{
				if (filterModel.Filter != null && !WalletBlocks.ContainsValue(filterModel.BlockHash))
				{
					var matchFound = filterModel.Filter.MatchAny(KeyManager.GetKeys().Select(x => x.GetP2wpkhScript().ToCompressedBytes()), filterModel.FilterKey);
					if (matchFound)
					{
						BlockDownloader.QueueToDownload(filterModel.BlockHash);
						WalletBlocks.AddOrReplace(filterModel.BlockHeight, filterModel.BlockHash);
					}
				}
			}
		}

		public async Task InitializeAsync()
		{
			if (!BlockDownloader.IsRunning)
			{
				throw new NotSupportedException($"{nameof(BlockDownloader)} is not running.");
			}
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
					var matchFound = filterModel.Filter.MatchAny(KeyManager.GetKeys().Select(x => x.GetP2wpkhScript().ToCompressedBytes()), filterModel.FilterKey);
					if (matchFound)
					{
						BlockDownloader.QueueToDownload(filterModel.BlockHash);
						WalletBlocks.AddOrReplace(filterModel.BlockHeight, filterModel.BlockHash);
					}
				}

				foreach (var relevantBlock in WalletBlocks)
				{
					Block block = null;
					while ((block = BlockDownloader.GetBlock(relevantBlock.Value)) == null) // Wait until not downloaded.
					{
						await Task.Delay(100);
					}

					var keys = KeyManager.GetKeys().ToList();

					foreach (var tx in block.Transactions)
					{
						// If transaction received to any of the wallet keys:
						for (var i = 0; i < tx.Outputs.Count; i++)
						{
							var output = tx.Outputs[i];
							HdPubKey foundKey = keys.SingleOrDefault(x => x.GetP2wpkhScript() == output.ScriptPubKey);
							if (foundKey != default)
							{
								foundKey.KeyState = KeyState.Used;
								var coin = new SmartCoin(tx.GetHash(), i, output.ScriptPubKey, output.Value, tx.Inputs.ToTxoRefs().ToArray(), relevantBlock.Key, foundKey.Label, null);

								// Make sure there's always 21 clean keys generated and indexed.
								while (KeyManager.GetKeys(KeyState.Clean, foundKey.IsInternal()).Count() < 21)
								{
									KeyManager.GenerateNewKey("", KeyState.Clean, foundKey.IsInternal());
								}
							}
						}

						// If spends any of our coin
						for (var i = 0; i < tx.Inputs.Count; i++)
						{
							var input = tx.Inputs[i];

							var foundCoin = KnownCoins.SingleOrDefault(x => x.TransactionId == input.PrevOut.Hash && x.Index == input.PrevOut.N);
							if (foundCoin != null)
							{
								foundCoin.SpenderTransactionId = tx.GetHash();
							}
						}
					}

					ProcessedBlocks.Add(block.GetHash());
				}
			}
		}

		public void Synchronize()
		{
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							if (!BlockDownloader.IsRunning)
							{
								Logger.LogError<WalletService>($"{nameof(BlockDownloader)} is not running.");
								await Task.Delay(1000);
								continue;
							}
							if (!IndexDownloader.IsRunning)
							{
								Logger.LogError<WalletService>($"{nameof(IndexDownloader)} is not running.");
								await Task.Delay(1000);
								continue;
							}

							await Task.Delay(1000); // dummmy wait for now (TODO)
						}
						catch (Exception ex)
						{
							Logger.LogDebug<WalletService>(ex);
						}
					}
				}
				finally
				{
					if (IsStopping)
					{
						Interlocked.Exchange(ref _running, 3);
					}
				}
			}
			);
		}

		public async Task StopAsync()
		{
			BlockDownloader.NewBlock -= BlockDownloader_NewBlock;
			IndexDownloader.NewFilter -= IndexDownloader_NewFilter;
			IndexDownloader.Reorged -= IndexDownloader_Reorged;
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			while (IsStopping)
			{
				await Task.Delay(50);
			}
		}
	}
}
