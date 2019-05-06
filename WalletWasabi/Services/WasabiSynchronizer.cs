using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class WasabiSynchronizer : IDisposable, INotifyPropertyChanged
	{
		#region MembersPropertiesEvents

		public SynchronizeResponse LastResponse { get; private set; }

		public WasabiClient WasabiClient { get; private set; }

		public Network Network { get; private set; }

		private Height _bestBlockchainHeight;

		public Height BestBlockchainHeight
		{
			get => _bestBlockchainHeight;

			private set
			{
				if (_bestBlockchainHeight != value)
				{
					_bestBlockchainHeight = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BestBlockchainHeight)));
				}
			}
		}

		private decimal _usdExchangeRate;

		/// <summary>
		/// The Bitcoin price in USD.
		/// </summary>
		public decimal UsdExchangeRate
		{
			get => _usdExchangeRate;

			private set
			{
				if (_usdExchangeRate != value)
				{
					_usdExchangeRate = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsdExchangeRate)));
				}
			}
		}

		private AllFeeEstimate _allFeeEstimate;

		public AllFeeEstimate AllFeeEstimate
		{
			get => _allFeeEstimate;

			private set
			{
				if (_allFeeEstimate != value)
				{
					_allFeeEstimate = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllFeeEstimate)));
				}
			}
		}

		private FilterModel _bestKnownFilter;

		public FilterModel BestKnownFilter
		{
			get => _bestKnownFilter;

			private set
			{
				if (_bestKnownFilter != value)
				{
					_bestKnownFilter = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BestKnownFilter)));
				}
			}
		}

		private TorStatus _torStatus;

		public TorStatus TorStatus
		{
			get => _torStatus;

			private set
			{
				if (_torStatus != value)
				{
					_torStatus = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TorStatus)));
				}
			}
		}

		private BackendStatus _backendStatus;

		public BackendStatus BackendStatus
		{
			get => _backendStatus;

			private set
			{
				if (_backendStatus != value)
				{
					_backendStatus = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackendStatus)));
				}
			}
		}

		public TimeSpan MaxRequestIntervalForMixing { get; set; }
		private long _blockRequests; // There are priority requests in queue.

		public bool AreRequestsBlocked() => Interlocked.Read(ref _blockRequests) == 1;

		public void BlockRequests() => Interlocked.Exchange(ref _blockRequests, 1);

		public void EnableRequests() => Interlocked.Exchange(ref _blockRequests, 0);

		public string IndexFilePath { get; private set; }
		private List<FilterModel> Index { get; set; }
		private object IndexLock { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public event EventHandler<FilterModel> Reorged;

		public event EventHandler<FilterModel> NewFilter;

		public event EventHandler<bool> ResponseArrivedIsGenSocksServFail;

		public event EventHandler<SynchronizeResponse> ResponseArrived;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Cancel { get; set; }

		#endregion MembersPropertiesEvents

		#region ConstructorsAndInitializers

		public WasabiSynchronizer(Network network, string indexFilePath, WasabiClient client)
		{
			CreateNew(network, indexFilePath, client);
		}

		public WasabiSynchronizer(Network network, string indexFilePath, Func<Uri> baseUriAction, IPEndPoint torSocks5EndPoint)
		{
			var client = new WasabiClient(baseUriAction, torSocks5EndPoint);
			CreateNew(network, indexFilePath, client);
		}

		public WasabiSynchronizer(Network network, string indexFilePath, Uri baseUri, IPEndPoint torSocks5EndPoint)
		{
			var client = new WasabiClient(baseUri, torSocks5EndPoint);
			CreateNew(network, indexFilePath, client);
		}

		private void CreateNew(Network network, string indexFilePath, WasabiClient client)
		{
			Network = Guard.NotNull(nameof(network), network);
			WasabiClient = Guard.NotNull(nameof(client), client);
			LastResponse = null;
			_running = 0;
			Cancel = new CancellationTokenSource();
			BestBlockchainHeight = Height.Unknown;
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath, trim: true);
			Index = new List<FilterModel>();
			IndexLock = new object();

			IoHelpers.EnsureContainingDirectoryExists(indexFilePath);
			if (File.Exists(IndexFilePath))
			{
				if (Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
					Index.Add(StartingFilter);
					IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
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
								var filter = FilterModel.FromHeightlessLine(line, height);
								height++;
								Index.Add(filter);
							}
						}
					}
					catch (FormatException)
					{
						// We found a corrupted entry. Stop here.
						// Fix the currupted file.
						IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
					}
				}
			}
			else
			{
				Index.Add(StartingFilter);
				IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
			}

			BestKnownFilter = Index.Last();
		}

		public void Start(TimeSpan requestInterval, TimeSpan feeQueryRequestInterval, int maxFiltersToSyncAtInitialization)
		{
			Guard.NotNull(nameof(requestInterval), requestInterval);
			Guard.MinimumAndNotNull(nameof(feeQueryRequestInterval), feeQueryRequestInterval, requestInterval);
			Guard.MinimumAndNotNull(nameof(maxFiltersToSyncAtInitialization), maxFiltersToSyncAtInitialization, 0);

			MaxRequestIntervalForMixing = requestInterval; // Let's start with this, it'll be modified from outside.

			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				try
				{
					DateTimeOffset lastFeeQueried = DateTimeOffset.UtcNow - feeQueryRequestInterval;
					bool ignoreRequestInterval = false;
					EnableRequests();
					while (IsRunning)
					{
						try
						{
							while (AreRequestsBlocked())
							{
								await Task.Delay(3000, Cancel.Token);
							}

							EstimateSmartFeeMode? estimateMode = null;
							TimeSpan elapsed = DateTimeOffset.UtcNow - lastFeeQueried;
							if (elapsed >= feeQueryRequestInterval)
							{
								estimateMode = EstimateSmartFeeMode.Conservative;
							}

							FilterModel startingFilter = BestKnownFilter;

							SynchronizeResponse response;
							try
							{
								if (!IsRunning)
								{
									return;
								}

								response = await WasabiClient.GetSynchronizeAsync(BestKnownFilter.BlockHash, maxFiltersToSyncAtInitialization, estimateMode, Cancel.Token).WithAwaitCancellationAsync(Cancel.Token, 300);
								// NOT GenSocksServErr
								BackendStatus = BackendStatus.Connected;
								TorStatus = TorStatus.Running;
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

							if (response.AllFeeEstimate != null && response.AllFeeEstimate.Estimations.Any())
							{
								lastFeeQueried = DateTimeOffset.UtcNow;
								AllFeeEstimate = response.AllFeeEstimate;
							}

							if (response.Filters.Count() == maxFiltersToSyncAtInitialization)
							{
								ignoreRequestInterval = true;
							}
							else
							{
								ignoreRequestInterval = false;
							}

							BestBlockchainHeight = response.BestHeight;
							ExchangeRate exchangeRate = response.ExchangeRates.FirstOrDefault();
							if (exchangeRate != default && exchangeRate.Rate != 0)
							{
								UsdExchangeRate = exchangeRate.Rate;
							}

							if (response.FiltersResponseState == FiltersResponseState.NewFilters)
							{
								List<FilterModel> filtersList = response.Filters.ToList(); // performance

								for (int i = 0; i < filtersList.Count; i++)
								{
									FilterModel filterModel;
									lock (IndexLock)
									{
										filterModel = filtersList[i];
										Index.Add(filterModel);
										BestKnownFilter = filterModel;
									}

									NewFilter?.Invoke(this, filterModel);
								}

								lock (IndexLock)
								{
									IoHelpers.SafeWriteAllLines(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
									var startingFilterHeightPlusOne = startingFilter.BlockHeight + 1;
									var bestKnownFilterHeight = BestKnownFilter.BlockHeight;
									if (startingFilterHeightPlusOne == bestKnownFilterHeight)
									{
										Logger.LogInfo<WasabiSynchronizer>($"Downloaded filter for block {startingFilterHeightPlusOne}.");
									}
									else
									{
										Logger.LogInfo<WasabiSynchronizer>($"Downloaded filters for blocks from {startingFilterHeightPlusOne} to {bestKnownFilterHeight}.");
									}
								}
							}
							else if (response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
							{
								// Reorg happened
								var reorgedFilter = BestKnownFilter;
								Logger.LogInfo<WasabiSynchronizer>($"REORG Invalid Block: {reorgedFilter.BlockHash}");
								// 1. Rollback index
								lock (IndexLock)
								{
									Index.RemoveLast();
									BestKnownFilter = Index.Last();
								}
								Reorged?.Invoke(this, reorgedFilter);

								// 2. Serialize Index. (Remove last line.)
								string[] lines = null;
								if (IoHelpers.TryGetSafestFileVersion(IndexFilePath, out var safestFileVerion))
								{
									lines = File.ReadAllLines(safestFileVerion);
								}
								IoHelpers.SafeWriteAllLines(IndexFilePath, lines.Take(lines.Length - 1).ToArray()); // It's not async for a reason, I think.

								ignoreRequestInterval = true;
							}
							else if (response.FiltersResponseState == FiltersResponseState.NoNewFilter)
							{
								// We are syced.
							}

							LastResponse = response;
							ResponseArrived?.Invoke(this, response);
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
						catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
						{
							Logger.LogTrace<WasabiSynchronizer>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogError<WasabiSynchronizer>(ex);
						}
						finally
						{
							if (IsRunning && !ignoreRequestInterval)
							{
								try
								{
									int delay = (int)Math.Min(requestInterval.TotalMilliseconds, MaxRequestIntervalForMixing.TotalMilliseconds);
									await Task.Delay(delay, Cancel.Token); // Ask for new index in every requestInterval.
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
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		#endregion ConstructorsAndInitializers

		#region Methods

		public static FilterModel GetStartingFilter(Network network)
		{
			if (network == Network.Main)
			{
				return FilterModel.FromHeightlessLine("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893:02832810ec08a0", GetStartingHeight(network));
			}
			if (network == Network.TestNet)
			{
				return FilterModel.FromHeightlessLine("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:017821b8", GetStartingHeight(network));
			}
			if (network == Network.RegTest)
			{
				return FilterModel.FromHeightlessLine("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", GetStartingHeight(network));
			}
			throw new NotSupportedException($"{network} is not supported.");
		}

		public FilterModel StartingFilter => GetStartingFilter(Network);

		public static Height GetStartingHeight(Network network) => IndexBuilderService.GetStartingHeight(network);

		public Height StartingHeight => GetStartingHeight(Network);

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

		public Height? TryGetHeight(uint256 blockHash)
		{
			lock (IndexLock)
			{
				return Index.FirstOrDefault(x => x.BlockHash == blockHash)?.BlockHeight;
			}
		}

		public int GetFiltersLeft()
		{
			if (BestBlockchainHeight == Height.Unknown || BestBlockchainHeight == Height.MemPool || BestKnownFilter.BlockHeight == Height.Unknown || BestKnownFilter.BlockHeight == Height.MemPool)
			{
				return -1;
			}
			return BestBlockchainHeight.Value - BestKnownFilter.BlockHeight.Value;
		}

		public IEnumerable<FilterModel> GetFilters()
		{
			lock (IndexLock)
			{
				return Index.ToList();
			}
		}

		public int CountFilters() => Index.Count;

		public Money GetFeeRate(int feeTarget)
		{
			return AllFeeEstimate.GetFeeRate(feeTarget);
		}

		#endregion Methods

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
					Cancel?.Cancel();
					while (IsStopping)
					{
						Task.Delay(50).GetAwaiter().GetResult(); // DO NOT MAKE IT ASYNC (.NET Core threading brainfart)
					}

					Cancel?.Dispose();
					WasabiClient?.Dispose();

					EnableRequests(); // Enable requests (it's possible something is being blocked outside the class by AreRequestsBlocked.
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
