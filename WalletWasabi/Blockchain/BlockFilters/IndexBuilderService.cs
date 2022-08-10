using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters;

public class IndexBuilderService
{
	private const long NotStarted = 0;
	private const long Running = 1;
	private const long Stopping = 2;
	private const long Stopped = 3;

	/// <summary>
	/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
	/// </summary>
	private long _serviceStatus;

	private long _workerCount;

	public IndexBuilderService((RpcPubkeyType[] filterType, string indexFilePath)[] indexes, IRPCClient rpc, BlockNotifier blockNotifier)
	{
		indexes = Guard.NotNullOrEmpty(nameof(indexes), indexes);
		RpcClient = Guard.NotNull(nameof(rpc), rpc);
		BlockNotifier = Guard.NotNull(nameof(blockNotifier), blockNotifier);
		_serviceStatus = NotStarted;

		var indices = new List<Index>();
		foreach (var idx in indexes)
		{
			var index = new Index(idx.filterType, RpcClient.Network, idx.indexFilePath);
			indices.Add(index);
		}
		Indices = indices;

		BlockNotifier.OnBlock += BlockNotifier_OnBlock;
	}

	public IRPCClient RpcClient { get; }
	public BlockNotifier BlockNotifier { get; }
	public IEnumerable<Index> Indices { get; }
	public bool IsRunning => Interlocked.Read(ref _serviceStatus) == Running;
	public bool IsStopping => Interlocked.Read(ref _serviceStatus) >= Stopping;
	public DateTimeOffset LastFilterBuildTime { get; set; }

	public void Synchronize()
	{
		Task.Run(async () =>
		{
			try
			{
				if (Interlocked.Read(ref _workerCount) >= 2)
				{
					return;
				}

				Interlocked.Increment(ref _workerCount);
				while (Interlocked.Read(ref _workerCount) != 1)
				{
					await Task.Delay(100).ConfigureAwait(false);
				}

				if (IsStopping)
				{
					return;
				}

				try
				{
					Interlocked.Exchange(ref _serviceStatus, Running);

					while (IsRunning)
					{
						try
						{
							SyncInfo syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);

							foreach (var index in Indices)
							{
								uint currentHeight;
								uint256? currentHash = null;
								using (await index.AsyncLock.LockAsync().ConfigureAwait(false))
								{
									if (index.Filters.Count != 0)
									{
										var lastIndex = index.Filters[^1];
										currentHeight = lastIndex.Header.Height;
										currentHash = lastIndex.Header.BlockHash;
									}
									else
									{
										currentHash = index.StartingHeight == 0
											? uint256.Zero
											: await RpcClient.GetBlockHashAsync((int)index.StartingHeight - 1).ConfigureAwait(false);
										currentHeight = index.StartingHeight - 1;
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
										Interlocked.Exchange(ref _serviceStatus, NotStarted);
										return;
									}
									else
									{
										// Knots is catching up give it a 10 seconds
										await Task.Delay(10000).ConfigureAwait(false);
										continue;
									}
								}

								uint nextHeight = currentHeight + 1;
								uint256 blockHash = await RpcClient.GetBlockHashAsync((int)nextHeight).ConfigureAwait(false);
								VerboseBlockInfo block = await RpcClient.GetVerboseBlockAsync(blockHash).ConfigureAwait(false);

								// Check if we are still on the best chain,
								// if not rewind filters till we find the fork.
								if (currentHash != block.PrevBlockHash)
								{
									Logger.LogWarning("Reorg observed on the network.");
									await index.ReorgOneAsync().ConfigureAwait(false);

									// Skip the current block.
									continue;
								}

								var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, nextHeight, block.BlockTime);

								await index.BuildAndSerializeFilterAsync(block, smartHeader).ConfigureAwait(false);

								// If not close to the tip, just log debug.
								if (syncInfo.BlockCount - nextHeight <= 3 || nextHeight % 100 == 0)
								{
									Logger.LogInfo($"Created filter for block: {nextHeight}.");
								}
								else
								{
									Logger.LogDebug($"Created filter for block: {nextHeight}.");
								}
							}
							LastFilterBuildTime = DateTimeOffset.UtcNow;
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);

							// Pause the while loop for a while to not flood logs in case of permanent error.
							await Task.Delay(1000).ConfigureAwait(false);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _serviceStatus, Stopped, Stopping); // If IsStopping, make it stopped.
					Interlocked.Decrement(ref _workerCount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Synchronization attempt failed to start: {ex}");
			}
		});
	}

	private async Task<SyncInfo> GetSyncInfoAsync()
	{
		var bcinfo = await RpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
		var pbcinfo = new SyncInfo(bcinfo);
		return pbcinfo;
	}

	private void BlockNotifier_OnBlock(object? sender, Block e)
	{
		try
		{
			// Run sync every time a block notification arrives. Synchronizer will stop when it finishes.
			Synchronize();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public async Task StopAsync()
	{
		if (BlockNotifier is { })
		{
			BlockNotifier.OnBlock -= BlockNotifier_OnBlock;
		}

		Interlocked.CompareExchange(ref _serviceStatus, Stopping, Running); // If running, make it stopping.

		while (Interlocked.CompareExchange(ref _serviceStatus, Stopped, NotStarted) == 2)
		{
			await Task.Delay(50).ConfigureAwait(false);
		}
	}

	public (Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(IEnumerable<RpcPubkeyType> scriptTypes, uint256 knownHash, int count, out bool found)
		=> Indices.First(
			x => x
				.FilterType
				.OrderBy(x => x)
				.SequenceEqual(scriptTypes.OrderBy(x => x)))
				.GetFilterLinesExcluding(knownHash, count, out found);
}
