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
using System.IO;
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

		public string BlocksFolderPath { get; }
		private AsyncLock BlocksFolderLock { get; }

		private List<uint256> BlocksToDownload { get; }
		private AsyncLock BlocksToDownloadLock { get; }

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;
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
				using (BlocksFolderLock.Lock())
				{
					return Directory.EnumerateFiles(BlocksFolderPath).Count();
				}
			}
		}

		public BlockDownloader(NodesGroup nodes, string blocksFolderPath)
		{
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			BlocksFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(blocksFolderPath), blocksFolderPath, trim: true);

			_running = 0;
			BlocksToDownload = new List<uint256>();
			BlocksToDownloadLock = new AsyncLock();
			BlocksFolderLock = new AsyncLock();

			if (Directory.Exists(BlocksFolderPath))
			{
				foreach(var blockFilePath in Directory.EnumerateFiles(BlocksFolderPath))
				{
					var blockBytes = File.ReadAllBytes(blockFilePath);
					var block = new Block(blockBytes);
				}
			}
			else
			{
				Directory.CreateDirectory(BlocksFolderPath);
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

								if (block == null)
								{
									Logger.LogInfo<BlockDownloader>($"Disconnected node, because couldn't parse received block.");
									node.DisconnectAsync("Couldn't parse block.");
									continue;
								}

								if (!block.Check())
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

							using (BlocksFolderLock.Lock())
							using (BlocksToDownloadLock.Lock())
							{
								BlocksToDownload.Remove(hash);
								var path = Path.Combine(BlocksFolderPath, hash.ToString());
								await File.WriteAllBytesAsync(path, block.ToBytes());
							}
						}
						catch (Exception ex)
						{
							Logger.LogDebug<BlockDownloader>(ex);
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
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			while (IsStopping)
			{
				await Task.Delay(50);
			}
		}

		public void QueToDownload(uint256 hash)
		{
			using (BlocksFolderLock.Lock())
			using (BlocksToDownloadLock.Lock())
			{
				var filePaths = Directory.EnumerateFiles(BlocksFolderPath);
				var fileNames = filePaths.Select(x => Path.GetFileName(x));
				var hashes = fileNames.Select(x => new uint256(x));

				if (!hashes.Contains(hash))
				{
					if (!BlocksToDownload.Contains(hash))
					{
						BlocksToDownload.Add(hash);
					}
				}
			}
		}

		/// <remarks>
		/// Use it at reorgs.
		/// </remarks>
		public void TryRemove(uint256 hash)
		{
			using (BlocksFolderLock.Lock())
			using (BlocksToDownloadLock.Lock())
			{
				if(BlocksToDownload.Contains(hash))
				{
					BlocksToDownload.Remove(hash);
				}

				var filePaths = Directory.EnumerateFiles(BlocksFolderPath);
				var fileNames = filePaths.Select(x => Path.GetFileName(x));
				var hashes = fileNames.Select(x => new uint256(x));

				if (hashes.Contains(hash))
				{
					File.Delete(Path.Combine(BlocksFolderPath, hash.ToString()));
				}
			}
		}

		/// <returns>null if don't have, ques it if not qued</returns>
		public Block GetBlock(uint256 hash)
		{
			using (BlocksFolderLock.Lock())
			{
				foreach(var filePath in Directory.EnumerateFiles(BlocksFolderPath))
				{
					var fileName = Path.GetFileName(filePath);
					if(hash == new uint256(fileName))
					{
						var blockBytes = File.ReadAllBytes(filePath);
						var block = new Block(blockBytes);
						return block;
					}
				}
			}

			QueToDownload(hash);
			return default;
		}
	}
}
