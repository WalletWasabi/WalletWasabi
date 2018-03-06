using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.TorSocks5;
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

namespace MagicalCryptoWallet.Services
{
    public class IndexDownloader
    {
		public Network Network { get; }

		public TorHttpClient Client { get; }

		public string IndexFilePath { get; }
		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }
		
		public static Height GetStartingHeight(Network network) => IndexBuilderService.GetStartingHeight(network);
		public Height StartingHeight => GetStartingHeight(Network);

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

		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		public IndexDownloader(Network network, string indexFilePath, Uri indexHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			Client = new TorHttpClient(indexHostUri, torSocks5EndPoint, isolateStream: false);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);

			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			_running = 0;

			var indexDir = Path.GetDirectoryName(IndexFilePath);
			Directory.CreateDirectory(indexDir);
			if (File.Exists(IndexFilePath))
			{
				if (Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
				}
				else
				{
					int height = StartingHeight.Value;
					foreach (var line in File.ReadAllLines(IndexFilePath))
					{
						var filter = FilterModel.FromLine(line, new Height(height));
						height++;
						Index.Add(filter);
					}
				}
			}
		}

		public void Syncronize()
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

						FilterModel bestKnownFilter;
						using (await IndexLock.LockAsync())
						{
							if(Index.Count == 0)
							{
								bestKnownFilter = StartingFilter;
							}
							else
							{
								bestKnownFilter = Index.Last();
							}
						}

						var response = await Client.SendAsync(HttpMethod.Get, $"/api/v1/btc/Blockchain/filters/{bestKnownFilter.BlockHash}");

						if(response.StatusCode == HttpStatusCode.NoContent)
						{
							continue;
						}
						if(response.StatusCode == HttpStatusCode.OK)
						{
							var filters = await response.Content.ReadAsJsonAsync<List<string>>();

							for(int i = 0; i < filters.Count; i++)
							{
								var filterModel = FilterModel.FromLine(filters[i], new Height(bestKnownFilter.BlockHeight.Value + i + 1));
								
								await File.AppendAllLinesAsync(IndexFilePath, new[] { filterModel.ToLine() });
								using (await IndexLock.LockAsync())
								{
									Index.Add(filterModel);
								}
							}

							Logger.LogInfo<IndexDownloader>($"Downloaded filters for blocks from {bestKnownFilter.BlockHeight} to {Index.Last().BlockHeight}.");

							continue;
						}
						else if(response.StatusCode == HttpStatusCode.NotFound)
						{
							// Reorg happened
							Logger.LogInfo<IndexDownloader>($"REORG Invalid Block: {bestKnownFilter.BlockHash}");
							// 1. Rollback index
							using (await IndexLock.LockAsync())
							{
								Index.RemoveAt(Index.Count - 1);
							}

							// 2. Serialize Index. (Remove last line.)
							var lines = File.ReadAllLines(IndexFilePath);
							File.WriteAllLines(IndexFilePath, lines.Take(lines.Length - 1).ToArray());

							// 3. Skip the last valid block.
							continue;
						}
						else
						{
							var error = await response.Content.ReadAsStringAsync();
							throw new HttpRequestException($"{response.StatusCode.ToReasonString()}: {error}");
						}
					}
					catch (Exception ex)
					{
						Logger.LogDebug<IndexDownloader>(ex);
					}
					finally
					{
						await Task.Delay(TimeSpan.FromSeconds(30)); // Ask for new index every 30 seconds.
					}
				}
			});
		}

		public void Stop()
		{
			Interlocked.Exchange(ref _running, 0);
		}
	}
}
