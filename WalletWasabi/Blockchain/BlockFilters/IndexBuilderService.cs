using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters
{
	public class IndexBuilderService : PeriodicRunner
	{
		public IndexBuilderService(IRPCClient rpc, BlockNotifier blockNotifier, string indexFilePath) : base(TimeSpan.FromMinutes(1))
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			BlockNotifier = Guard.NotNull(nameof(blockNotifier), blockNotifier);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);

			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			StartingHeight = SmartHeader.GetStartingHeader(RpcClient.Network).Height;

			IoHelpers.EnsureContainingDirectoryExists(IndexFilePath);
			if (File.Exists(IndexFilePath))
			{
				if (RpcClient.Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
				}
				else
				{
					foreach (var line in File.ReadAllLines(IndexFilePath))
					{
						var filter = FilterModel.FromLine(line);
						Index.Add(filter);
					}
				}
			}

			BlockNotifier.OnBlock += BlockNotifier_OnBlock;
			StartAsync(CancellationTokenSource.Token);
		}

		public static byte[][] DummyScript { get; } = new byte[][] { ByteHelpers.FromHex("0009BBE4C2D17185643765C265819BF5261755247D") };

		public IRPCClient RpcClient { get; }
		public BlockNotifier BlockNotifier { get; }
		public string IndexFilePath { get; }
		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }
		public uint StartingHeight { get; }

		public bool IsRunning => !CancellationTokenSource.IsCancellationRequested;
		public DateTimeOffset LastFilterBuildTime { get; set; }

		public CancellationTokenSource CancellationTokenSource { get; init; } = new();

		public static GolombRiceFilter CreateDummyEmptyFilter(uint256 blockHash)
		{
			return new GolombRiceFilterBuilder()
				.SetKey(blockHash)
				.SetP(20)
				.SetM(1 << 20)
				.AddEntries(DummyScript)
				.Build();
		}

		public void Synchronize()
		{
			TriggerRound();
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				while (!cancel.IsCancellationRequested)
				{
					try
					{
						SyncInfo syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);

						uint currentHeight;
						uint256? currentHash = null;
						using (await IndexLock.LockAsync(cancel))
						{
							if (Index.Count != 0)
							{
								var lastIndex = Index[^1];
								currentHeight = lastIndex.Header.Height;
								currentHash = lastIndex.Header.BlockHash;
							}
							else
							{
								currentHash = StartingHeight == 0
									? uint256.Zero
									: await RpcClient.GetBlockHashAsync((int)StartingHeight - 1, cancel).ConfigureAwait(false);
								currentHeight = StartingHeight - 1;
							}
						}

						var coreNotSynced = !syncInfo.IsCoreSynchronized;
						var tipReached = syncInfo.BlockCount == currentHeight;
						var isTimeToRefresh = DateTimeOffset.UtcNow - syncInfo.BlockchainInfoUpdated > TimeSpan.FromMinutes(5);
						if (coreNotSynced || tipReached || isTimeToRefresh)
						{
							syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);
						}

						// If wasabi filter height is the same as core we may be done.
						if (syncInfo.BlockCount == currentHeight)
						{
							// Check that core is fully synced
							if (syncInfo.IsCoreSynchronized && !syncInfo.InitialBlockDownload)
							{
								// Mark the process notstarted, so it can be started again
								// and finally block can mark it as stopped.
								return;
							}
							else
							{
								// Knots is catching up give it a 10 seconds
								await Task.Delay(10000, cancel).ConfigureAwait(false);
								continue;
							}
						}

						uint nextHeight = currentHeight + 1;
						uint256 blockHash = await RpcClient.GetBlockHashAsync((int)nextHeight, cancel).ConfigureAwait(false);
						VerboseBlockInfo block = await RpcClient.GetVerboseBlockAsync(blockHash, cancel).ConfigureAwait(false);

						// Check if we are still on the best chain,
						// if not rewind filters till we find the fork.
						if (currentHash != block.PrevBlockHash)
						{
							Logger.LogWarning("Reorg observed on the network.");

							await ReorgOneAsync().ConfigureAwait(false);

							// Skip the current block.
							continue;
						}

						var filter = BuildFilterForBlock(block);

						var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, nextHeight, block.BlockTime);
						var filterModel = new FilterModel(smartHeader, filter);

						await File.AppendAllLinesAsync(IndexFilePath, new[] { filterModel.ToLine() }, CancellationToken.None).ConfigureAwait(false);

						using (await IndexLock.LockAsync(cancel))
						{
							Index.Add(filterModel);
						}

						// If not close to the tip, just log debug.
						if (syncInfo.BlockCount - nextHeight <= 3 || nextHeight % 100 == 0)
						{
							Logger.LogInfo($"Created filter for block: {nextHeight}.");
						}
						else
						{
							Logger.LogDebug($"Created filter for block: {nextHeight}.");
						}
						LastFilterBuildTime = DateTimeOffset.UtcNow;
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Synchronization attempt failed to start: {ex}");
			}
		}

		internal static GolombRiceFilter BuildFilterForBlock(VerboseBlockInfo block)
		{
			var scripts = FetchScripts(block);

			if (scripts.Any())
			{
				return new GolombRiceFilterBuilder()
					.SetKey(block.Hash)
					.SetP(20)
					.SetM(1 << 20)
					.AddEntries(scripts.Select(x => x.ToCompressedBytes()))
					.Build();
			}
			else
			{
				// We cannot have empty filters, because there was a bug in GolombRiceFilterBuilder that evaluates empty filters to true.
				// And this must be fixed in a backwards compatible way, so we create a fake filter with a random scp instead.
				return CreateDummyEmptyFilter(block.Hash);
			}
		}

		private static List<Script> FetchScripts(VerboseBlockInfo block)
		{
			var scripts = new List<Script>();

			foreach (var tx in block.Transactions)
			{
				foreach (var input in tx.Inputs)
				{
					if (input.PrevOutput is { PubkeyType: RpcPubkeyType.TxWitnessV0Keyhash })
					{
						scripts.Add(input.PrevOutput.ScriptPubKey);
					}
				}

				foreach (var output in tx.Outputs)
				{
					if (output is { PubkeyType: RpcPubkeyType.TxWitnessV0Keyhash })
					{
						scripts.Add(output.ScriptPubKey);
					}
				}
			}

			return scripts;
		}

		private async Task ReorgOneAsync()
		{
			// 1. Rollback index
			using (await IndexLock.LockAsync())
			{
				Logger.LogInfo($"REORG invalid block: {Index[^1].Header.BlockHash}");
				Index.RemoveLast();
			}

			// 2. Serialize Index. (Remove last line.)
			var lines = await File.ReadAllLinesAsync(IndexFilePath).ConfigureAwait(false);
			await File.WriteAllLinesAsync(IndexFilePath, lines.Take(lines.Length - 1).ToArray()).ConfigureAwait(false);
		}

		private async Task<SyncInfo> GetSyncInfoAsync()
		{
			var bcinfo = await RpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
			var pbcinfo = new SyncInfo(bcinfo);
			return pbcinfo;
		}

		private void BlockNotifier_OnBlock(object? sender, Block e)
		{
			// Run sync every time a block notification arrives.
			TriggerRound();
		}

		public (Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(uint256 bestKnownBlockHash, int count, out bool found)
		{
			using (IndexLock.Lock())
			{
				found = false; // Only build the filter list from when the known hash is found.
				var filters = new List<FilterModel>();
				foreach (var filter in Index)
				{
					if (found)
					{
						filters.Add(filter);
						if (filters.Count >= count)
						{
							break;
						}
					}
					else
					{
						if (filter.Header.BlockHash == bestKnownBlockHash)
						{
							found = true;
						}
					}
				}

				if (Index.Count == 0)
				{
					return (Height.Unknown, Enumerable.Empty<FilterModel>());
				}
				else
				{
					return ((int)Index[^1].Header.Height, filters);
				}
			}
		}

		public FilterModel GetLastFilter()
		{
			using (IndexLock.Lock())
			{
				return Index[^1];
			}
		}

		public override async Task StopAsync(CancellationToken cancellationToken = default)
		{
			if (BlockNotifier is { })
			{
				BlockNotifier.OnBlock -= BlockNotifier_OnBlock;
			}

			await base.StopAsync(cancellationToken).ConfigureAwait(false);

			CancellationTokenSource?.Dispose();
		}
	}
}
