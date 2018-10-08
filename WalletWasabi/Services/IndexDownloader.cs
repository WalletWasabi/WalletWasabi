using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Exceptions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class IndexDownloader : IDisposable
	{
		public Network Network { get; }

		public FilterModel BestKnownFilter { get; private set; }

		private Height _bestHeight;

		public Height BestHeight
		{
			get => _bestHeight;

			private set
			{
				if (_bestHeight != value)
				{
					_bestHeight = value;
					BestHeightChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<bool> ResponseArrivedIsGenSocksServFail;

		public event EventHandler<Height> BestHeightChanged;

		private TorStatus _torStatus;

		public TorStatus TorStatus
		{
			get => _torStatus;

			private set
			{
				if (_torStatus != value)
				{
					_torStatus = value;
					TorStatusChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<TorStatus> TorStatusChanged;

		private BackendStatus _backendStatus;

		public BackendStatus BackendStatus
		{
			get => _backendStatus;

			private set
			{
				if (_backendStatus != value)
				{
					_backendStatus = value;
					BackendStatusChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<BackendStatus> BackendStatusChanged;

		public int GetFiltersLeft()
		{
			if (BestHeight == Height.Unknown || BestHeight == Height.MemPool || BestKnownFilter.BlockHeight == Height.Unknown || BestKnownFilter.BlockHeight == Height.MemPool)
			{
				return -1;
			}
			return BestHeight.Value - BestKnownFilter.BlockHeight.Value;
		}

		public WasabiClient WasabiClient { get; }

		public string IndexFilePath { get; }
		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }

		public static Height GetStartingHeight(Network network) => IndexBuilderService.GetStartingHeight(network);

		public Height StartingHeight => GetStartingHeight(Network);

		public event EventHandler<uint256> Reorged;

		public event EventHandler<FilterModel> NewFilter;

		public static FilterModel GetStartingFilter(Network network)
		{
			if (network == Network.Main)
			{
				return FilterModel.FromLine("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893:02832810ec08a0", GetStartingHeight(network));
			}
			if (network == Network.TestNet)
			{
				return FilterModel.FromLine("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:017821b8", GetStartingHeight(network));
			}
			if (network == Network.RegTest)
			{
				return FilterModel.FromLine("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", GetStartingHeight(network));
			}
			throw new NotSupportedException($"{network} is not supported.");
		}

		public FilterModel StartingFilter => GetStartingFilter(Network);

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Cancel { get; }

		public IndexDownloader(Network network, string indexFilePath, Uri indexHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			WasabiClient = new WasabiClient(indexHostUri, torSocks5EndPoint);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);

			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			_running = 0;
			Cancel = new CancellationTokenSource();

			IoHelpers.EnsureContainingDirectoryExists(indexFilePath);
			if (File.Exists(IndexFilePath))
			{
				if (Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
					Index.Add(StartingFilter);
					IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToLine()));
				}
				else
				{
					var height = StartingHeight;
					try
					{
						if (IoHelpers.TryGetSafestFileVersion(IndexFilePath, out var safestFileVerion))
						{
							foreach (var line in File.ReadAllLines(safestFileVerion))
							{
								var filter = FilterModel.FromLine(line, height);
								height++;
								Index.Add(filter);
							}
						}
					}
					catch (FormatException)
					{
						// We found a corrupted entry. Stop here.
						// Fix the currupted file.
						IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToLine()));
					}
				}
			}
			else
			{
				Index.Add(StartingFilter);
				IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToLine()));
			}

			BestKnownFilter = Index.Last();
			BestHeight = Height.Unknown; // At this point we don't know it.
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
						var delayNextRequest = false;
						try
						{
							// If stop was requested return.
							if (!IsRunning) return;

							FilterModel startingFilter = BestKnownFilter;

							FiltersResponse filtersResponse = null;
							try
							{
								filtersResponse = await WasabiClient.GetFiltersAsync(BestKnownFilter.BlockHash, 1000, Cancel.Token).WithAwaitCancellationAsync(Cancel.Token, 300);
								// NOT GenSocksServErr
								DoNotGenSocksServFail();
							}
							catch (ConnectionException ex)
							{
								TorStatus = TorStatus.NotRunning;
								BackendStatus = BackendStatus.NotConnected;
								HandleIfGenSocksServFail(ex);

								throw;
							}
							catch (TorSocks5FailureResponseException ex)
							{
								TorStatus = TorStatus.Running;
								BackendStatus = BackendStatus.NotConnected;

								HandleIfGenSocksServFail(ex);

								throw;
							}
							catch (Exception ex)
							{
								TorStatus = TorStatus.Running;
								BackendStatus = BackendStatus.Connected;
								HandleIfGenSocksServFail(ex);

								throw;
							}
							BackendStatus = BackendStatus.Connected;
							TorStatus = TorStatus.Running;

							if (filtersResponse is null) // no-content, we are synced
							{
								BestHeight = BestKnownFilter.BlockHeight;
								continue;
							}
							BestHeight = filtersResponse.BestHeight;

							using (await IndexLock.LockAsync(Cancel.Token))
							{
								var filtersList = filtersResponse.Filters.ToList(); // performance
								for (int i = 0; i < filtersList.Count; i++)
								{
									var filterModel = FilterModel.FromLine(filtersList[i], BestKnownFilter.BlockHeight + 1);

									Index.Add(filterModel);
									BestKnownFilter = filterModel;
									NewFilter?.Invoke(this, filterModel);
								}

								IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToLine()));
								Logger.LogInfo<IndexDownloader>($"Downloaded filters for blocks from {startingFilter.BlockHeight + 1} to {BestKnownFilter.BlockHeight}.");
							}

							if (BestHeight == BestKnownFilter.BlockHeight) // if we're synced
							{
								delayNextRequest = true;
							}

							continue;
						}
						catch (TaskCanceledException ex)
						{
							Logger.LogTrace<CcjClient>(ex);
						}
						catch (OperationCanceledException ex)
						{
							Logger.LogTrace<CcjClient>(ex);
						}
						catch (HttpRequestException ex) when (ex.Message.StartsWith(HttpStatusCode.NotFound.ToReasonString()))
						{
							// Reorg happened
							var reorgedHash = BestKnownFilter.BlockHash;
							Logger.LogInfo<IndexDownloader>($"REORG Invalid Block: {reorgedHash}");
							// 1. Rollback index
							using (await IndexLock.LockAsync(Cancel.Token))
							{
								Index.RemoveAt(Index.Count - 1);
								BestKnownFilter = Index.Last();
							}

							Reorged?.Invoke(this, reorgedHash);

							// 2. Serialize Index. (Remove last line.)
							string[] lines = null;
							if (IoHelpers.TryGetSafestFileVersion(IndexFilePath, out var safestFileVerion))
							{
								lines = File.ReadAllLines(safestFileVerion);
							}
							IoHelpers.SafeWriteAllLines(IndexFilePath, lines.Take(lines.Length - 1).ToArray()); // It's not async for a reason, I think.

							// 3. Skip the last valid block.
							continue;
						}
						catch (ConnectionException ex)
						{
							Logger.LogError<CcjClient>(ex);
							try
							{
								await Task.Delay(3000, Cancel.Token); // Give other threads time to do stuff.
							}
							catch (TaskCanceledException ex2)
							{
								Logger.LogTrace<CcjClient>(ex2);
							}
						}
						catch (Exception ex)
						{
							Logger.LogError<IndexDownloader>(ex);
						}
						finally
						{
							if (IsRunning && delayNextRequest)
							{
								try
								{
									await Task.Delay(requestInterval, Cancel.Token); // Ask for new index in every requestInterval.
								}
								catch (TaskCanceledException ex)
								{
									Logger.LogTrace<CcjClient>(ex);
								}
							}
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

		private void HandleIfGenSocksServFail(Exception ex)
		{
			// IS GenSocksServFail?
			if (ex.ToString().Contains("GeneralSocksServerFailure", StringComparison.OrdinalIgnoreCase))
			{
				// IS GenSocksServFail
				DoGenSocksServFail();
			}
			else
			{
				// NOT GenSocksServFail
				DoNotGenSocksServFail();
			}
		}

		private void DoGenSocksServFail()
		{
			ResponseArrivedIsGenSocksServFail?.Invoke(this, true);
		}

		private void DoNotGenSocksServFail()
		{
			ResponseArrivedIsGenSocksServFail?.Invoke(this, false);
		}

		public Height GetHeight(uint256 blockHash)
		{
			using (IndexLock.Lock())
			{
				var single = Index.Single(x => x.BlockHash == blockHash);
				return single.BlockHeight;
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

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (IsRunning)
					{
						Interlocked.Exchange(ref _running, 2);
					}
					Cancel?.Cancel();
					while (IsStopping)
					{
						Task.Delay(50).GetAwaiter().GetResult(); // DO NOT MAKE IT ASYNC (.NET Core threading brainfart)
					}

					Cancel?.Dispose();
					WasabiClient?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
