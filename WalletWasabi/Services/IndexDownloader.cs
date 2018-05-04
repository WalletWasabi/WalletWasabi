using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.TorSocks5;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients;

namespace WalletWasabi.Services
{
    public class IndexDownloader
    {
		public Network Network { get; }

		public TorHttpClient TorClient { get; }

		public WasabiApiClient Client { get; }

		public string IndexFilePath { get; }
		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }
		
		public static Height GetStartingHeight(Network network) => IndexBuilderService.GetStartingHeight(network);
		public Height StartingHeight => GetStartingHeight(Network);

		public event EventHandler<uint256> Reorged;
		private void OnReorg(uint256 invalidBlockHash) => Reorged?.Invoke(this, invalidBlockHash);

		public event EventHandler<FilterModel> NewFilter;
		private void OnNewFilter(FilterModel filter) => NewFilter?.Invoke(this, filter);

		public static FilterModel GetStartingFilter(Network network)
		{
			if (network == Network.Main)
			{
				return FilterModel.FromLine("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893:2:43:11288322B003", GetStartingHeight(network));
			}
			else if (network == Network.TestNet)
			{
				return FilterModel.FromLine("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:1:21:6E081E", GetStartingHeight(network));
			}
			else if (network == Network.RegTest)
			{
				return FilterModel.FromLine("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", GetStartingHeight(network));
			}
			else
			{
				throw new NotSupportedException($"{network} is not supported.");
			}
		}
		public FilterModel StartingFilter => GetStartingFilter(Network);

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public IndexDownloader(Network network, string indexFilePath, Uri indexHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			TorClient = new TorHttpClient(indexHostUri, torSocks5EndPoint, isolateStream: false);
			Client = new WasabiApiClient(TorClient);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);

			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			_running = 0;

			var indexDir = Path.GetDirectoryName(IndexFilePath);
			if (!string.IsNullOrEmpty(indexDir))
			{
				Directory.CreateDirectory(indexDir);
			}
			if (File.Exists(IndexFilePath))
			{
				if (Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
					Index.Add(StartingFilter);
					File.WriteAllLines(IndexFilePath, Index.Select(x => x.ToLine()));
				}
				else
				{
					var height = StartingHeight;
					foreach (var line in File.ReadAllLines(IndexFilePath))
					{
						var filter = FilterModel.FromLine(line, height);
						height++;
						Index.Add(filter);
					}
				}
			}
			else
			{
				Index.Add(StartingFilter);
				File.WriteAllLines(IndexFilePath, Index.Select(x => x.ToLine()));
			}
		}

		public void Synchronize(TimeSpan requestInterval)
		{
			Guard.NotNull(nameof(requestInterval), requestInterval);
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

							FilterModel bestKnownFilter;
							using (await IndexLock.LockAsync())
							{
								bestKnownFilter = Index.Last();
							}

							try
							{
								var filtersEnumerable = await Client.GetFiltersAsync(bestKnownFilter.BlockHash, 50);
								var filters = filtersEnumerable.ToList();

								using (await IndexLock.LockAsync())
								{
									for (int i = 0; i < filters.Count; i++)
									{
										var filterModel = FilterModel.FromLine(filters[i], bestKnownFilter.BlockHeight + i + 1);

										Index.Add(filterModel);
										OnNewFilter(filterModel);
									}

									if (filters.Count == 1) // minor optimization
									{
										await File.AppendAllLinesAsync(IndexFilePath, new[] { Index.Last().ToLine() });
									}
									else
									{
										await File.WriteAllLinesAsync(IndexFilePath, Index.Select(x => x.ToLine()));
									}

									Logger.LogInfo<IndexDownloader>($"Downloaded filters for blocks from {bestKnownFilter.BlockHeight.Value + 1} to {Index.Last().BlockHeight}.");
								}
							}
							catch(ApiClientException e)
							{
								if(e.ErrorCode == ApiClientErrorCode.FilterNotFound){
									// Reorg happened
									var reorgedHash = bestKnownFilter.BlockHash;
									Logger.LogInfo<IndexDownloader>($"REORG Invalid Block: {reorgedHash}");
									// 1. Rollback index
									using (await IndexLock.LockAsync())
									{
										Index.RemoveAt(Index.Count - 1);
									}

									OnReorg(reorgedHash);

									// 2. Serialize Index. (Remove last line.)
									var lines = File.ReadAllLines(IndexFilePath);
									File.WriteAllLines(IndexFilePath, lines.Take(lines.Length - 1).ToArray());
								}
								// 3. Skip the last valid block.
								continue;
							}
						}
						catch (Exception ex)
						{
							Logger.LogError<IndexDownloader>(ex);
						}
						finally
						{
							await Task.Delay(requestInterval); // Ask for new index in every requestInterval.
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
			});
		}
		
		public Height GetHeight(uint256 blockHash)
		{
			using (IndexLock.Lock())
			{
				var single = Index.Single(x => x.BlockHash == blockHash);
				if(single != null)
				{
					return single.BlockHeight;
				}
				else
				{
					return Height.Unknown;
				}
			}
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

		public FilterModel GetBestFilter()
		{
			using (IndexLock.Lock())
			{
				return Index.Last();
			}
		}

		public IEnumerable<FilterModel> GetFiltersIncluding(uint256 blockHash)
		{
			using (IndexLock.Lock())
			{
				var found = false;
				foreach (var filter in Index)
				{
					if (filter.BlockHash == blockHash)
					{
						found = true;
					}

					if (found)
					{
						yield return filter;
					}
				}
			}
		}

		public IEnumerable<FilterModel> GetFiltersIncluding(Height height)
		{
			using (IndexLock.Lock())
			{
				var found = false;
				foreach (var filter in Index)
				{
					if (filter.BlockHeight == height)
					{
						found = true;
					}

					if (found)
					{
						yield return filter;
					}
				}
			}
		}
	}
}
