using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using System.Linq;
using HBitcoin.Models;
using System.Threading;
using System.Diagnostics;
using System.Net;
using ConcurrentCollections;
using System.Collections.Concurrent;

namespace HBitcoin.FullBlockSpv
{
	public class BlockDownloader
	{
		private ConcurrentHashSet<Height> BlocksToDownload = new ConcurrentHashSet<Height>();
		private ConcurrentDictionary<Height, Block> DownloadedBlocks = new ConcurrentDictionary<Height, Block>();

		public void Clear()
		{
			BlocksToDownload.Clear();
			DownloadedBlocks.Clear();
		}

		public async Task<Block> TakeBlockAsync(Height height, Height lookAheadHeight, CancellationToken ctsToken)
		{

			for (int h = height.Value; h <= lookAheadHeight; h++)
			{
				if (!DownloadedBlocks.ContainsKey(new Height(h)))
				{
					BlocksToDownload.Add(new Height(h));
				}
			}

			while (!DownloadedBlocks.ContainsKey(height))
			{
				if (ctsToken.IsCancellationRequested) return null;
				await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
			}

			if (ctsToken.IsCancellationRequested) return null;

			DownloadedBlocks.TryRemove(height, out Block block);
			return block;
		}

		public async Task StartAsync(CancellationToken ctsToken)
		{
			while (WalletJob.ConnectedNodeCount < 3)
			{
				await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
				if (ctsToken.IsCancellationRequested) return;
			}

			var tasks = new HashSet<Task>
			{
				StartDownloadingWithFastestAsync(ctsToken),
				StartDownloadingWithIteratingAync(ctsToken)
			};

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}

		private IPEndPoint _fastestNodeEndPoint;
		private async Task StartDownloadingWithFastestAsync(CancellationToken ctsToken)
		{
			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;
					SetFastestNode();
					if (ctsToken.IsCancellationRequested) return;

					Node fastestNode = WalletJob.Nodes.ConnectedNodes.FirstOrDefault(x => x.RemoteSocketEndpoint.Equals(_fastestNodeEndPoint));
					if (fastestNode == default(Node)) continue;
					await DownloadNextBlocks(fastestNode, ctsToken, 3).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(StartDownloadingWithFastestAsync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}

		private SemaphoreSlim _sem = new SemaphoreSlim(1, 1);

		private async Task DownloadNextBlocks(Node node, CancellationToken ctsToken, int maxBlocksToDownload = 1)
		{
			var heights = new List<Height>();
			try
			{
				await _sem.WaitAsync(ctsToken).ConfigureAwait(false);

				if (BlocksToDownload.Count == 0)
				{
					await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
					return;
				}

				if (BlocksToDownload.Count < maxBlocksToDownload * 2)
				{
					maxBlocksToDownload = 1;
				}

				for (int i = 0; i < maxBlocksToDownload; i++)
				{
					// todo check if we have that much block
					var height = BlocksToDownload.Min();
					BlocksToDownload.TryRemove(height);
					heights.Add(height);
				}
			}
			finally
			{
				_sem.Release();
			}
			try
			{
				var headers = new HashSet<ChainedBlock>();
				foreach(var height in heights)
				{
					WalletJob.TryGetHeader(height, out ChainedBlock neededHeader);
					headers.Add(neededHeader);
				}

				var delayMinutes = heights.Count;
				var timeoutToken = new CancellationTokenSource(TimeSpan.FromMinutes(delayMinutes)).Token;
				var downloadCtsToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, ctsToken).Token;

				HashSet<Block> blocks = null;
				try
				{
					blocks = new HashSet<Block>(await Task.Run(() => node.GetBlocks(headers.ToArray(), downloadCtsToken)).ConfigureAwait(false));
				}
				catch
				{
					if (timeoutToken.IsCancellationRequested)
					{
						node.DisconnectAsync($"Block download time > {delayMinutes}min");
					}
					else node.DisconnectAsync("Block download failed");
					blocks = null;
				}
				if (blocks == null)
				{
					foreach (var height in heights)
					{
						BlocksToDownload.Add(height);
					}
				}
				else
				{
					int i = 0;
					foreach (var block in blocks)
					{
						DownloadedBlocks.AddOrReplace(heights[i], block);
						i++;
					}
				}
			}
			catch
			{
				try
				{
					await _sem.WaitAsync(ctsToken).ConfigureAwait(false);

					foreach (var height in heights)
					{
						BlocksToDownload.Add(height);
					}
				}
				finally
				{
					_sem.Release();
				}
			}
		}

		private void SetFastestNode()
		{
			var performances = new Dictionary<IPEndPoint, long>();
			foreach (var node in WalletJob.Nodes.ConnectedNodes)
			{
				var speed = (long)(node.Counter.ReadenBytes / node.Counter.Elapsed.TotalSeconds);
				performances.Add(node.RemoteSocketEndpoint, speed);
			}

			// find where the value is max
			_fastestNodeEndPoint = performances.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
		}

		private async Task StartDownloadingWithIteratingAync(CancellationToken ctsToken)
		{
			var downloadTasks = new HashSet<Task>();
			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;

					foreach (var node in WalletJob.Nodes.ConnectedNodes)
					{
						if (node.RemoteSocketEndpoint != _fastestNodeEndPoint)
						{
							downloadTasks.Add(DownloadNextBlocks(node, ctsToken, 1));
						}
						if (downloadTasks.Count >= 2)
						{
							await Task.WhenAny(downloadTasks).ConfigureAwait(false);
						}
						RemoveCompletedTasks(downloadTasks);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(StartDownloadingWithIteratingAync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}

		private static void RemoveCompletedTasks(HashSet<Task> downloadTasks)
		{
			var tasksToRemove = new HashSet<Task>();
			foreach (var task in downloadTasks)
			{
				if (task.IsCompleted)
				{
					tasksToRemove.Add(task);
				}
			}
			foreach (var task in tasksToRemove)
			{
				downloadTasks.Remove(task);
			}
		}
	}
}
