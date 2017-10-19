using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Net;
using ConcurrentCollections;
using System.Collections.Concurrent;
using HiddenWallet.Models;
using Nito.AsyncEx;

namespace HiddenWallet.FullSpv
{
	public class BlockDownloader
	{
		private ConcurrentHashSet<Height> BlocksToDownload = new ConcurrentHashSet<Height>();
		private ConcurrentDictionary<Height, Block> DownloadedBlocks = new ConcurrentDictionary<Height, Block>();
        private WalletJob _walletJob;

        public BlockDownloader(WalletJob walletJob)
        {
            _walletJob = walletJob ?? throw new ArgumentNullException(nameof(walletJob));
        }

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
				await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
			}

			if (ctsToken.IsCancellationRequested) return null;

			DownloadedBlocks.TryRemove(height, out Block block);
			return block;
		}

        public async Task StartAsync(CancellationToken ctsToken)
        {
            var tasks = new HashSet<Task>();
            try
            {
                while (_walletJob.ConnectedNodeCount < 3)
                {
                    await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
                    if (ctsToken.IsCancellationRequested) return;
                }

                tasks = new HashSet<Task>
            {
                StartDownloadingWithFastestAsync(ctsToken),
                StartDownloadingWithIteratingAsync(ctsToken)
            };

                await Task.WhenAll(tasks);
            }
            finally
            {
                foreach(var task in tasks)
                {
                    task?.Dispose();
                }
            }
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

					Node fastestNode = _walletJob.Nodes.ConnectedNodes.FirstOrDefault(x => x.RemoteSocketEndpoint.Equals(_fastestNodeEndPoint));
					if (fastestNode == default(Node)) continue;
					await DownloadNextBlocksAsync(fastestNode, ctsToken, 3);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(StartDownloadingWithFastestAsync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}

        private readonly AsyncLock _asyncLock = new AsyncLock();

		private async Task DownloadNextBlocksAsync(Node node, CancellationToken ctsToken, int maxBlocksToDownload = 1)
		{
			var heights = new List<Height>();
			using(await _asyncLock.LockAsync())
			{
				if (BlocksToDownload.Count == 0)
				{
					await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
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
			try
			{
				var headers = new HashSet<ChainedBlock>();
				foreach(var height in heights)
				{
					var neededHeader = await _walletJob.TryGetHeaderAsync(height);
					headers.Add(neededHeader);
				}

				var delayMinutes = heights.Count;
                using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(delayMinutes)))
                {
                    var timeoutToken = cancellationTokenSource.Token;
                    var downloadCtsToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, ctsToken).Token;

                    HashSet<Block> blocks = null;
                    try
                    {
                        blocks = new HashSet<Block>(await Task.Run(() => node.GetBlocks(headers.ToArray(), downloadCtsToken)));
                    }
                    catch (Exception)
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
            }
            catch (Exception)
            {
                using(_asyncLock.Lock())
                {
                    foreach (var height in heights)
                    {
                        BlocksToDownload.Add(height);
                    }
                }
            }
        }

		private void SetFastestNode()
		{
			var performances = new Dictionary<IPEndPoint, long>();
			foreach (var node in _walletJob.Nodes.ConnectedNodes)
			{
				var speed = (long)(node.Counter.ReadenBytes / node.Counter.Elapsed.TotalSeconds);
				performances.Add(node.RemoteSocketEndpoint, speed);
			}

			// find where the value is max
			_fastestNodeEndPoint = performances.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
		}

		private async Task StartDownloadingWithIteratingAsync(CancellationToken ctsToken)
		{
			var downloadTasks = new HashSet<Task>();
			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;

					foreach (var node in _walletJob.Nodes.ConnectedNodes)
					{
						if (node.RemoteSocketEndpoint != _fastestNodeEndPoint)
						{
							downloadTasks.Add(DownloadNextBlocksAsync(node, ctsToken, 1));
						}
						if (downloadTasks.Count >= 2)
						{
							await Task.WhenAny(downloadTasks);
						}
						RemoveCompletedTasks(downloadTasks);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(StartDownloadingWithIteratingAsync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}

		private void RemoveCompletedTasks(HashSet<Task> downloadTasks)
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
