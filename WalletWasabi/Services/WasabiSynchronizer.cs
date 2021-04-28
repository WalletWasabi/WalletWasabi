using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class WasabiSynchronizer : NotifyPropertyChangedBase, IThirdPartyFeeProvider
	{
		private const long StateNotStarted = 0;

		private const long StateRunning = 1;

		private const long StateStopping = 2;

		private const long StateStopped = 3;

		private decimal _usdExchangeRate;

		private TorStatus _torStatus;

		private BackendStatus _backendStatus;

		/// <summary>
		/// Value can be any of: <see cref="StateNotStarted"/>, <see cref="StateRunning"/>, <see cref="StateStopping"/> and <see cref="StateStopped"/>.
		/// </summary>
		private long _running;

		private long _blockRequests; // There are priority requests in queue.

		/// <param name="httpClientFactory">The class takes ownership of the instance.</param>
		public WasabiSynchronizer(BitcoinStore bitcoinStore, HttpClientFactory httpClientFactory)
		{
			LastResponse = null;
			_running = StateNotStarted;
			BitcoinStore = bitcoinStore;
			HttpClientFactory = httpClientFactory;
			WasabiClient = httpClientFactory.SharedWasabiClient;

			StopCts = new CancellationTokenSource();
		}

		#region EventsPropertiesMembers

		public event EventHandler<bool>? ResponseArrivedIsGenSocksServFail;

		public event EventHandler<SynchronizeResponse>? ResponseArrived;

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

		public SynchronizeResponse? LastResponse { get; private set; }

		/// <summary><see cref="WasabiSynchronizer"/> is responsible for disposing of this object.</summary>
		public HttpClientFactory HttpClientFactory { get; }

		public WasabiClient WasabiClient { get; }

		/// <summary>
		/// Gets the Bitcoin price in USD.
		/// </summary>
		public decimal UsdExchangeRate
		{
			get => _usdExchangeRate;
			private set => RaiseAndSetIfChanged(ref _usdExchangeRate, value);
		}

		public TorStatus TorStatus
		{
			get => _torStatus;
			private set => RaiseAndSetIfChanged(ref _torStatus, value);
		}

		public BackendStatus BackendStatus
		{
			get => _backendStatus;
			private set
			{
				if (RaiseAndSetIfChanged(ref _backendStatus, value))
				{
					BackendStatusChangedAt = DateTimeOffset.UtcNow;
				}
			}
		}

		private DateTimeOffset BackendStatusChangedAt { get; set; } = DateTimeOffset.UtcNow;
		public TimeSpan BackendStatusChangedSince => DateTimeOffset.UtcNow - BackendStatusChangedAt;

		public TimeSpan MaxRequestIntervalForMixing { get; set; }

		public BitcoinStore BitcoinStore { get; }

		public bool IsRunning => Interlocked.Read(ref _running) == StateRunning;

		/// <summary>
		/// Cancellation token source for stopping <see cref="WasabiSynchronizer"/>.
		/// </summary>
		private CancellationTokenSource StopCts { get; }

		public AllFeeEstimate? LastAllFeeEstimate => LastResponse?.AllFeeEstimate;

		public bool InError => BackendStatus != BackendStatus.Connected;

		public bool AreRequestsBlocked() => Interlocked.Read(ref _blockRequests) == 1;

		public void BlockRequests() => Interlocked.Exchange(ref _blockRequests, 1);

		public void EnableRequests() => Interlocked.Exchange(ref _blockRequests, 0);

		#endregion EventsPropertiesMembers

		#region Initializers

		public void Start(TimeSpan requestInterval, int maxFiltersToSyncAtInitialization)
		{
			Logger.LogTrace($"> {nameof(requestInterval)}={requestInterval}, {nameof(maxFiltersToSyncAtInitialization)}={maxFiltersToSyncAtInitialization}");

			Guard.NotNull(nameof(requestInterval), requestInterval);
			Guard.MinimumAndNotNull(nameof(maxFiltersToSyncAtInitialization), maxFiltersToSyncAtInitialization, 0);

			MaxRequestIntervalForMixing = requestInterval; // Let's start with this, it'll be modified from outside.

			if (Interlocked.CompareExchange(ref _running, StateRunning, StateNotStarted) != StateNotStarted)
			{
				return;
			}

			Task.Run(async () =>
			{
				Logger.LogTrace("> Wasabi synchronizer thread starts.");

				try
				{
					bool ignoreRequestInterval = false;
					EnableRequests();
					while (IsRunning)
					{
						try
						{
							while (AreRequestsBlocked())
							{
								await Task.Delay(3000, StopCts.Token).ConfigureAwait(false);
							}

							SynchronizeResponse response;

							var lastUsedApiVersion = WasabiClient.ApiVersion;
							try
							{
								if (!IsRunning)
								{
									return;
								}

								response = await WasabiClient
									.GetSynchronizeAsync(BitcoinStore.SmartHeaderChain.TipHash, maxFiltersToSyncAtInitialization, EstimateSmartFeeMode.Conservative, StopCts.Token)
									.WithAwaitCancellationAsync(StopCts.Token, 300)
									.ConfigureAwait(false);

								// NOT GenSocksServErr
								BackendStatus = BackendStatus.Connected;
								TorStatus = TorStatus.Running;
								DoNotGenSocksServFail();
							}
							catch (HttpRequestException ex) when (ex.InnerException is TorException innerEx)
							{
								TorStatus = innerEx is TorConnectionException ? TorStatus.NotRunning : TorStatus.Running;
								BackendStatus = BackendStatus.NotConnected;
								HandleIfGenSocksServFail(ex);
								HandleIfGenSocksServFail(innerEx);
								throw;
							}
							catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
							{
								TorStatus = TorStatus.Running;
								BackendStatus = BackendStatus.NotConnected;
								try
								{
									// Backend API version might be updated meanwhile. Trying to update the versions.
									var result = await WasabiClient.CheckUpdatesAsync(StopCts.Token).ConfigureAwait(false);

									// If the backend is compatible and the Api version updated then we just used the wrong API.
									if (result.BackendCompatible && lastUsedApiVersion != WasabiClient.ApiVersion)
									{
										// Next request will be fine, do not throw exception.
										ignoreRequestInterval = true;
										continue;
									}
									else
									{
										throw;
									}
								}
								catch (Exception x)
								{
									HandleIfGenSocksServFail(x);
									throw;
								}
							}
							catch (Exception ex)
							{
								TorStatus = TorStatus.Running;
								BackendStatus = BackendStatus.NotConnected;
								HandleIfGenSocksServFail(ex);
								throw;
							}

							// If it's not fully synced or reorg happened.
							if (response.Filters.Count() == maxFiltersToSyncAtInitialization || response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
							{
								ignoreRequestInterval = true;
							}
							else
							{
								ignoreRequestInterval = false;
							}
							ExchangeRate? exchangeRate = response.ExchangeRates.FirstOrDefault();
							if (exchangeRate is { Rate: > 0 })
							{
								UsdExchangeRate = exchangeRate.Rate;
							}

							LastResponse = response;
							ResponseArrived?.Invoke(this, response);
							if (response.AllFeeEstimate is { } allFeeEstimate)
							{
								AllFeeEstimateArrived?.Invoke(this, allFeeEstimate);
							}
						}
						catch (OperationCanceledException)
						{
							Logger.LogInfo("Wasabi Synchronizer execution was canceled.");
						}
						catch (TorConnectionException ex)
						{
							Logger.LogError(ex);
							try
							{
								await Task.Delay(3000, StopCts.Token).ConfigureAwait(false); // Give other threads time to do stuff.
							}
							catch (TaskCanceledException ex2)
							{
								Logger.LogTrace(ex2);
							}
						}
						catch (TimeoutException ex)
						{
							Logger.LogTrace(ex);
						}
						catch (Exception ex)
						{
							Logger.LogError(ex);
						}
						finally
						{
							if (IsRunning && !ignoreRequestInterval)
							{
								try
								{
									int delay = (int)Math.Min(requestInterval.TotalMilliseconds, MaxRequestIntervalForMixing.TotalMilliseconds);
									await Task.Delay(delay, StopCts.Token).ConfigureAwait(false); // Ask for new index in every requestInterval.
								}
								catch (TaskCanceledException ex)
								{
									Logger.LogTrace(ex);
								}
							}
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, StateStopped, StateStopping); // If IsStopping, make it stopped.
				}

				Logger.LogTrace("< Wasabi synchronizer thread ends.");
			});

			Logger.LogTrace("<");
		}

		#endregion Initializers

		#region Methods

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

		#endregion Methods

		/// <summary>
		/// Stops <see cref="WasabiSynchronizer"/>.
		/// </summary>
		/// <remarks>The method is supposed to be called just once.</remarks>
		public async Task StopAsync()
		{
			Logger.LogTrace(">");

			Interlocked.CompareExchange(ref _running, StateStopping, StateRunning); // If running, make it stopping.
			StopCts.Cancel();

			while (Interlocked.CompareExchange(ref _running, StateStopped, StateNotStarted) == StateStopping)
			{
				await Task.Delay(50).ConfigureAwait(false);
			}

			StopCts.Dispose();

			EnableRequests(); // Enable requests (it's possible something is being blocked outside the class by AreRequestsBlocked.

			Logger.LogTrace("<");
		}
	}
}
