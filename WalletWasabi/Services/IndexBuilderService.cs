using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Services
{
	public class IndexBuilderService
	{
		private class ActionHistoryHelper
		{
			public enum Operation
			{
				Add,
				Remove
			}

			private List<ActionItem> ActionHistory { get; }

			public ActionHistoryHelper()
			{
				ActionHistory = new List<ActionItem>();
			}

			public class ActionItem
			{
				public Operation Action { get; }
				public OutPoint OutPoint { get; }
				public Script Script { get; }

				public ActionItem(Operation action, OutPoint outPoint, Script script)
				{
					Action = action;
					OutPoint = outPoint;
					Script = script;
				}
			}

			public void ClearActionHistory()
			{
				ActionHistory.Clear();
			}

			public void StoreAction(ActionItem actionItem)
			{
				ActionHistory.Add(actionItem);
			}

			public void StoreAction(Operation action, OutPoint outpoint, Script script)
			{
				StoreAction(new ActionItem(action, outpoint, script));
			}

			public void Rollback(Dictionary<OutPoint, Script> toRollBack)
			{
				for (var i = ActionHistory.Count - 1; i >= 0; i--)
				{
					ActionItem act = ActionHistory[i];
					switch (act.Action)
					{
						case Operation.Add:
							toRollBack.Remove(act.OutPoint);
							break;

						case Operation.Remove:
							toRollBack.Add(act.OutPoint, act.Script);
							break;

						default:
							throw new ArgumentOutOfRangeException();
					}
				}
				ActionHistory.Clear();
			}
		}

		private class SyncInfo
		{
			public BlockchainInfo BlockchainInfo { get; }
			public int BlockCount { get; }
			public DateTimeOffset BlockchainInfoUpdated { get; }
			public bool IsCoreSynchornized { get; }

			public SyncInfo(BlockchainInfo bcinfo)
			{
				Guard.NotNull(nameof(bcinfo), bcinfo);
				BlockCount = (int)bcinfo.Blocks;
				int headerCount = (int)bcinfo.Headers;
				BlockchainInfoUpdated = DateTimeOffset.UtcNow;
				IsCoreSynchornized = BlockCount == headerCount;
			}
		}

		public RPCClient RpcClient { get; }
		public TrustedNodeNotifyingBehavior TrustedNodeNotifyingBehavior { get; }
		public string IndexFilePath { get; }
		public string Bech32UtxoSetFilePath { get; }

		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }

		private Dictionary<OutPoint, Script> Bech32UtxoSet { get; }
		private List<ActionHistoryHelper> Bech32UtxoSetHistory { get; }

		private Height StartingHeight { get; set; }

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		public IndexBuilderService(RPCClient rpc, TrustedNodeNotifyingBehavior trustedNodeNotifyingBehavior, string indexFilePath, string bech32UtxoSetFilePath)
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			TrustedNodeNotifyingBehavior = Guard.NotNull(nameof(trustedNodeNotifyingBehavior), trustedNodeNotifyingBehavior);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);
			Bech32UtxoSetFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(bech32UtxoSetFilePath), bech32UtxoSetFilePath);

			Bech32UtxoSet = new Dictionary<OutPoint, Script>();
			Bech32UtxoSetHistory = new List<ActionHistoryHelper>(capacity: 100);
			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			StartingHeight = StartingFilters.GetStartingHeight(RpcClient.Network);

			_running = 0;

			IoHelpers.EnsureContainingDirectoryExists(IndexFilePath);
			if (File.Exists(IndexFilePath))
			{
				if (RpcClient.Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
				}
				else
				{
					var height = StartingHeight;
					foreach (var line in File.ReadAllLines(IndexFilePath))
					{
						var filter = FilterModel.FromHeightlessLine(line, height);
						height++;
						Index.Add(filter);
					}
				}
			}

			IoHelpers.EnsureContainingDirectoryExists(bech32UtxoSetFilePath);
			if (File.Exists(bech32UtxoSetFilePath))
			{
				if (RpcClient.Network == Network.RegTest)
				{
					File.Delete(bech32UtxoSetFilePath); // RegTest is not a global ledger, better to delete it.
				}
				else
				{
					foreach (var line in File.ReadAllLines(Bech32UtxoSetFilePath))
					{
						var parts = line.Split(':');

						var txHash = new uint256(parts[0]);
						var nIn = int.Parse(parts[1]);
						var script = new Script(ByteHelpers.FromHex(parts[2]), true);
						Bech32UtxoSet.Add(new OutPoint(txHash, nIn), script);
					}
				}
			}

			TrustedNodeNotifyingBehavior.BlockInv += TrustedNodeNotifyingBehavior_BlockInv;
		}

		private long _runner;

		public void Synchronize()
		{
			Task.Run(async () =>
			{
				try
				{
					if (Interlocked.Read(ref _runner) >= 2)
					{
						return;
					}

					Interlocked.Increment(ref _runner);
					while (Interlocked.Read(ref _runner) != 1)
					{
						await Task.Delay(100);
					}

					if (Interlocked.Read(ref _running) >= 2)
					{
						return;
					}

					try
					{
						Interlocked.Exchange(ref _running, 1);

						var isImmature = false; // The last 100 blocks are reorgable. (Assume it is mature at first.)
						SyncInfo syncInfo = null;
						while (IsRunning)
						{
							try
							{
								// If we did not yet initialized syncInfo, do so.
								if (syncInfo is null)
								{
									syncInfo = await GetSyncInfoAsync();
								}

								Height heightToRequest = StartingHeight;
								uint256 currentHash = null;
								using (await IndexLock.LockAsync())
								{
									if (Index.Count != 0)
									{
										var lastIndex = Index.Last();
										heightToRequest = lastIndex.BlockHeight + 1;
										currentHash = lastIndex.BlockHash;
									}
								}

								// If not synchronized or already 5 min passed since last update, get the latest blockchain info.
								if (!syncInfo.IsCoreSynchornized || (syncInfo.BlockchainInfoUpdated - DateTimeOffset.UtcNow) > TimeSpan.FromMinutes(5))
								{
									syncInfo = await GetSyncInfoAsync();
								}

								if (syncInfo.BlockCount - heightToRequest <= 100)
								{
									// Both Wasabi and our Core node is in sync. Start doing stuff through P2P from now on.
									if (syncInfo.IsCoreSynchornized && syncInfo.BlockCount == heightToRequest - 1)
									{
										syncInfo = await GetSyncInfoAsync();
										// Double it to make sure not to accidentally miss any notification.
										if (syncInfo.IsCoreSynchornized && syncInfo.BlockCount == heightToRequest - 1)
										{
											// Mark the process notstarted, so it can be started again and finally block can mark it is stopped.
											Interlocked.Exchange(ref _running, 0);
											return;
										}
									}

									// Mark the synchronizing process is working with immature blocks from now on.
									isImmature = true;
								}

								Block block = await RpcClient.GetBlockAsync(heightToRequest);

								// Reorg check, except if we're requesting the starting height, because then the "currentHash" wouldn't exist.

								if (heightToRequest != StartingHeight && currentHash != block.Header.HashPrevBlock)
								{
									// Reorg can happen only when immature. (If it'd not be immature, that'd be a huge issue.)
									if (isImmature)
									{
										await ReorgOneAsync();
									}
									else
									{
										Logger.LogCritical("This is something serious! Over 100 block reorg is noticed! We cannot handle that!");
									}

									// Skip the current block.
									continue;
								}

								if (isImmature)
								{
									PrepareBech32UtxoSetHistory();
								}

								var scripts = new HashSet<Script>();

								foreach (var tx in block.Transactions)
								{
									// If stop was requested return.
									// Because this tx iteration can take even minutes
									// It does not need to be accessed with a thread safe fashion with Interlocked through IsRunning, this may have some performance benefit
									if (_running != 1)
									{
										return;
									}

									for (int i = 0; i < tx.Outputs.Count; i++)
									{
										var output = tx.Outputs[i];
										if (output.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
										{
											var outpoint = new OutPoint(tx.GetHash(), i);
											Bech32UtxoSet.Add(outpoint, output.ScriptPubKey);
											if (isImmature)
											{
												Bech32UtxoSetHistory.Last().StoreAction(ActionHistoryHelper.Operation.Add, outpoint, output.ScriptPubKey);
											}
											scripts.Add(output.ScriptPubKey);
										}
									}

									foreach (var input in tx.Inputs)
									{
										OutPoint prevOut = input.PrevOut;
										if (Bech32UtxoSet.TryGetValue(prevOut, out Script foundScript))
										{
											Bech32UtxoSet.Remove(prevOut);
											if (isImmature)
											{
												Bech32UtxoSetHistory.Last().StoreAction(ActionHistoryHelper.Operation.Remove, prevOut, foundScript);
											}
											scripts.Add(foundScript);
										}
									}
								}

								GolombRiceFilter filter = null;
								if (scripts.Count != 0)
								{
									filter = new GolombRiceFilterBuilder()
										.SetKey(block.GetHash())
										.SetP(20)
										.SetM(1 << 20)
										.AddEntries(scripts.Select(x => x.ToCompressedBytes()))
										.Build();
								}

								var filterModel = new FilterModel
								{
									BlockHash = block.GetHash(),
									BlockHeight = heightToRequest,
									Filter = filter
								};

								await File.AppendAllLinesAsync(IndexFilePath, new[] { filterModel.ToHeightlessLine() });
								using (await IndexLock.LockAsync())
								{
									Index.Add(filterModel);
								}
								if (File.Exists(Bech32UtxoSetFilePath))
								{
									File.Delete(Bech32UtxoSetFilePath);
								}
								await File.WriteAllLinesAsync(Bech32UtxoSetFilePath, Bech32UtxoSet
									.Select(entry => entry.Key.Hash + ":" + entry.Key.N + ":" + ByteHelpers.ToHex(entry.Value.ToCompressedBytes())));

								// If not close to the tip, just log debug.
								// Use height.Value instead of simply height, because it cannot be negative height.
								if (syncInfo.BlockCount - heightToRequest.Value <= 3 || heightToRequest % 100 == 0)
								{
									Logger.LogInfo($"Created filter for block: {heightToRequest}.");
								}
								else
								{
									Logger.LogDebug($"Created filter for block: {heightToRequest}.");
								}
							}
							catch (Exception ex)
							{
								Logger.LogDebug(ex);
							}
						}
					}
					finally
					{
						Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
						Interlocked.Decrement(ref _runner);
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
			var bcinfo = await RpcClient.GetBlockchainInfoAsync();
			var pbcinfo = new SyncInfo(bcinfo);
			return pbcinfo;
		}

		private void TrustedNodeNotifyingBehavior_BlockInv(object sender, uint256 e)
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

		private async Task ReorgOneAsync()
		{
			// 1. Rollback index
			using (await IndexLock.LockAsync())
			{
				Logger.LogInfo($"REORG invalid block: {Index.Last().BlockHash}");
				Index.RemoveLast();
			}

			// 2. Serialize Index. (Remove last line.)
			var lines = await File.ReadAllLinesAsync(IndexFilePath);
			await File.WriteAllLinesAsync(IndexFilePath, lines.Take(lines.Length - 1).ToArray());

			// 3. Rollback Bech32UtxoSet
			if (Bech32UtxoSetHistory.Count != 0)
			{
				Bech32UtxoSetHistory.Last().Rollback(Bech32UtxoSet); // The Bech32UtxoSet MUST be recovered to its previous state.
				Bech32UtxoSetHistory.RemoveLast();

				// 4. Serialize Bech32UtxoSet.
				await File.WriteAllLinesAsync(Bech32UtxoSetFilePath, Bech32UtxoSet
					.Select(entry => entry.Key.Hash + ":" + entry.Key.N + ":" + ByteHelpers.ToHex(entry.Value.ToCompressedBytes())));
			}
		}

		private void PrepareBech32UtxoSetHistory()
		{
			if (Bech32UtxoSetHistory.Count >= 100)
			{
				Bech32UtxoSetHistory.RemoveFirst();
			}
			Bech32UtxoSetHistory.Add(new ActionHistoryHelper());
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
						if (filter.BlockHash == bestKnownBlockHash)
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
					return (Index.Last().BlockHeight, filters);
				}
			}
		}

		public FilterModel GetLastFilter()
		{
			return Index.Last();
		}

		public async Task StopAsync()
		{
			if (TrustedNodeNotifyingBehavior != null)
			{
				TrustedNodeNotifyingBehavior.BlockInv -= TrustedNodeNotifyingBehavior_BlockInv;
			}

			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.

			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}
		}
	}
}
