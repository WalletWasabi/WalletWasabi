using ConcurrentCollections;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using NBitcoin;
using NBitcoin.Protocol;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Services
{
	public class BlockDownloader
	{
		public NodesGroup Nodes { get; }

		private List<uint256> BlocksToDownload { get; }
		private AsyncLock BlocksToDownloadLock { get; }
		private Dictionary<uint256, Block> DownloadedBlocks { get; }
		private AsyncLock DownloadedBlocksLock { get; }

		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public int NumberOfBlocksToDownload
		{
			get
			{
				using (BlocksToDownloadLock.Lock())
				{
					return BlocksToDownload.Count;
				}
			}
		}
		public int NumberOfDownloadedBlocks
		{
			get
			{
				using (DownloadedBlocksLock.Lock())
				{
					return DownloadedBlocks.Count;
				}
			}
		}

		public BlockDownloader(NodesGroup nodes)
		{
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			_running = 0;
			BlocksToDownload = new List<uint256>();
			BlocksToDownloadLock = new AsyncLock();
			DownloadedBlocks = new Dictionary<uint256, Block>();
			DownloadedBlocksLock = new AsyncLock();
		}

		public void Start()
		{
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				while (IsRunning)
				{
					try
					{
						// If stop was requested return.
						if (IsRunning == false) return;

						// If no connection, wait then continue.
						if (Nodes.ConnectedNodes.Count == 0)
						{
							await Task.Delay(10);
							continue;
						}
						if (IsRunning == false) return;

						uint256 hash = null;
						// If nothing to download, wait then continue.
						using (BlocksToDownloadLock.Lock())
						{
							if (BlocksToDownload.Count == 0)
							{
								await Task.Delay(100);
								continue;
							}
							else
							{
								hash = BlocksToDownload.First();
							}
						}
						if (IsRunning == false) return;

						Node node = Nodes.ConnectedNodes.RandomElement();
						if (node == default(Node))
						{
							await Task.Delay(10);
							continue;
						}
						if (!node.IsConnected)
						{
							await Task.Delay(10);
							continue;
						}

						Block block = null;

						try
						{
							using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(32))) // ADSL	512 kbit/s	00:00:32
							{
								block = node.GetBlocks(new uint256[] { hash }, cts.Token)?.Single();
							}

							if(block == null)
							{
								Logger.LogInfo<BlockDownloader>($"Disconnected node, because couldn't parse received block.");
								node.DisconnectAsync("Couldn't parse block.");
								continue;
							}

							if(!block.Check())
							{
								Logger.LogInfo<BlockDownloader>($"Disconnected node, because block invalid block received.");
								node.DisconnectAsync("Invalid block received.");
								continue;
							}
						}
						catch (TimeoutException)
						{
							Logger.LogInfo<BlockDownloader>($"Disconnected node, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (OperationCanceledException)
						{
							Logger.LogInfo<BlockDownloader>($"Disconnected node, because block download took too long.");
							node.DisconnectAsync("Block download took too long.");
							continue;
						}
						catch (Exception ex)
						{
							Logger.LogDebug<BlockDownloader>(ex);
							Logger.LogInfo<BlockDownloader>($"Disconnected node, because block download failed: {ex.Message}");
							node.DisconnectAsync("Block download failed.");
							continue;
						}

						using (DownloadedBlocksLock.Lock())
						using (BlocksToDownloadLock.Lock())
						{
							DownloadedBlocks.Add(hash, block);
							BlocksToDownload.Remove(hash);
						}
					}
					catch (Exception ex)
					{
						Logger.LogDebug<BlockDownloader>(ex);
					}
				}
			}
			);
		}

		public async Task StopAsync()
		{
			Interlocked.Exchange(ref _running, 0);

			while(IsRunning)
			{
				await Task.Delay(10);
			}
		}

		public void TryQueToDownload(uint256 hash)
		{
			using (DownloadedBlocksLock.Lock())
			using (BlocksToDownloadLock.Lock())
			{
				if (!DownloadedBlocks.ContainsKey(hash))
				{
					if (!BlocksToDownload.Contains(hash))
					{
						BlocksToDownload.Add(hash);
					}
				}
			}
		}
		
		/// <returns>null if don't have, ques it if not qued</returns>
		public Block TryGetBlock(uint256 hash)
		{
			using (DownloadedBlocksLock.Lock())
			{
				var block = DownloadedBlocks.SingleOrDefault(x => x.Key == hash).Value;
				if(block != default(Block))
				{
					return block;
				}
			}

			TryQueToDownload(hash);
			return default;
		}
	}
}
