using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
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
using WalletWasabi.Stores;
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

		public BitcoinStore BitcoinStore { get; private set; }

		public event PropertyChangedEventHandler PropertyChanged;

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

		public WasabiSynchronizer(Network network, BitcoinStore bitcoinStore, WasabiClient client)
		{
			CreateNew(network, bitcoinStore, client);
		}

		public WasabiSynchronizer(Network network, BitcoinStore bitcoinStore, Func<Uri> baseUriAction, IPEndPoint torSocks5EndPoint)
		{
			var client = new WasabiClient(baseUriAction, torSocks5EndPoint);
			CreateNew(network, bitcoinStore, client);
		}

		public WasabiSynchronizer(Network network, BitcoinStore bitcoinStore, Uri baseUri, IPEndPoint torSocks5EndPoint)
		{
			var client = new WasabiClient(baseUri, torSocks5EndPoint);
			CreateNew(network, bitcoinStore, client);
		}

		private void CreateNew(Network network, BitcoinStore bitcoinStore, WasabiClient client)
		{
			Network = Guard.NotNull(nameof(network), network);
			WasabiClient = Guard.NotNull(nameof(client), client);
			LastResponse = null;
			_running = 0;
			Cancel = new CancellationTokenSource();
			BestBlockchainHeight = Height.Unknown;
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
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

							SynchronizeResponse response;
							try
							{
								if (!IsRunning)
								{
									return;
								}

								var bestKnownFilter = await BitcoinStore.IndexStore.GetBestKnownFilterAsync();
								response = await WasabiClient.GetSynchronizeAsync(bestKnownFilter.BlockHash, maxFiltersToSyncAtInitialization, estimateMode, Cancel.Token).WithAwaitCancellationAsync(Cancel.Token, 300);
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
								var filters = response.Filters.ToArray(); // performance

								await BitcoinStore.IndexStore.AddNewFiltersAsync(filters);

								if (filters.Count() == 1)
								{
									Logger.LogInfo<WasabiSynchronizer>($"Downloaded filter for block {filters.First().BlockHeight}.");
								}
								else
								{
									Logger.LogInfo<WasabiSynchronizer>($"Downloaded filters for blocks from {filters.First().BlockHeight} to {filters.Last().BlockHeight}.");
								}
								_ = BitcoinStore.IndexStore.TryCommitToFileAsync(TimeSpan.FromSeconds(7), Cancel.Token);
							}
							else if (response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
							{
								// Reorg happened
								// 1. Rollback index
								FilterModel reorgedFilter = await BitcoinStore.IndexStore.RemoveLastFilterAsync();
								Logger.LogInfo<WasabiSynchronizer>($"REORG Invalid Block: {reorgedFilter.BlockHash}");

								ignoreRequestInterval = true;
								_ = BitcoinStore.IndexStore.TryCommitToFileAsync(TimeSpan.FromSeconds(7), Cancel.Token);
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
					await BitcoinStore.IndexStore.TryCommitToFileAsync(TimeSpan.Zero, CancellationToken.None);
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

		public async Task<int> GetFiltersLeftAsync()
		{
			var bestKnownFilter = await BitcoinStore.IndexStore.GetBestKnownFilterAsync();
			if (BestBlockchainHeight == Height.Unknown || BestBlockchainHeight == Height.MemPool || bestKnownFilter.BlockHeight == Height.Unknown || bestKnownFilter.BlockHeight == Height.MemPool)
			{
				return -1;
			}
			return BestBlockchainHeight.Value - bestKnownFilter.BlockHeight.Value;
		}

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
