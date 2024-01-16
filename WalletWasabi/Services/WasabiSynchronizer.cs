using NBitcoin.RPC;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class WasabiSynchronizer : PeriodicRunner, INotifyPropertyChanged, IThirdPartyFeeProvider, IWasabiBackendStatusProvider
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

	private bool _ignoreRequestInterval = false;

	public WasabiSynchronizer(TimeSpan requestInterval, int maxFiltersToSync, BitcoinStore bitcoinStore, WasabiHttpClientFactory httpClientFactory) : base(requestInterval)
	{
		RequestInterval = requestInterval;
		MaxFiltersToSync = maxFiltersToSync;

		LastResponse = null;
		_running = StateNotStarted;
		SmartHeaderChain = bitcoinStore.SmartHeaderChain;
		FilterProcessor = new FilterProcessor(bitcoinStore);
		HttpClientFactory = httpClientFactory;
		WasabiClient = httpClientFactory.SharedWasabiClient;
	}

	#region EventsPropertiesMembers

	public event EventHandler<bool>? ResponseArrivedIsGenSocksServFail;

	public event EventHandler<SynchronizeResponse>? ResponseArrived;

	public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Task completion source that is completed once a first synchronization request succeeds or fails.</summary>
	public TaskCompletionSource<bool> InitialRequestTcs { get; } = new();

	public SynchronizeResponse? LastResponse { get; private set; }
	public WasabiHttpClientFactory HttpClientFactory { get; }
	private WasabiClient WasabiClient { get; }

	/// <summary>Gets the Bitcoin price in USD.</summary>
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
	private TimeSpan RequestInterval { get; }
	private int MaxFiltersToSync { get; }
	private SmartHeaderChain SmartHeaderChain { get; }
	private FilterProcessor FilterProcessor { get; }

	public bool IsRunning => Interlocked.Read(ref _running) == StateRunning;

	/// <summary>Cancellation token source for stopping <see cref="WasabiSynchronizer"/>.</summary>
	private CancellationTokenSource StopCts { get; } = new();

	public AllFeeEstimate? LastAllFeeEstimate => LastResponse?.AllFeeEstimate;

	public bool InError => BackendStatus != BackendStatus.Connected;

	#endregion EventsPropertiesMembers

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		try
		{
			try
			{
				SynchronizeResponse response;

				ushort lastUsedApiVersion = WasabiClient.ApiVersion;
				try
				{
					response = await WasabiClient
						.GetSynchronizeAsync(SmartHeaderChain.TipHash, MaxFiltersToSync, EstimateSmartFeeMode.Conservative, StopCts.Token)
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
							_ignoreRequestInterval = true;
							return;
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
				if (response.Filters.Count() == MaxFiltersToSync || response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
				{
					_ignoreRequestInterval = true;
				}
				else
				{
					_ignoreRequestInterval = false;
				}
				ExchangeRate? exchangeRate = response.ExchangeRates.FirstOrDefault();
				if (exchangeRate is { Rate: > 0 })
				{
					UsdExchangeRate = exchangeRate.Rate;
				}

				await FilterProcessor.ProcessAsync((uint)response.BestHeight, response.FiltersResponseState, response.Filters).ConfigureAwait(false);

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
			catch (HttpRequestException ex) when (ex.InnerException is TorConnectionException)
			{
				// When stopping, we do not want to wait.
				if (!IsRunning)
				{
					Logger.LogTrace(ex);
					return;
				}

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
				if (IsRunning && !_ignoreRequestInterval)
				{
					try
					{
						await Task.Delay(RequestInterval, StopCts.Token).ConfigureAwait(false); // Ask for new index in every requestInterval.
					}
					catch (TaskCanceledException ex)
					{
						Logger.LogTrace(ex);
					}
				}
			}
		}
		finally
		{
			Interlocked.CompareExchange(ref _running, StateStopped, StateStopping); // If IsStopping, make it stopped.
			Logger.LogDebug("Synchronizer is fully stopped now.");
		}
	}

	#region Methods

	private void HandleIfGenSocksServFail(Exception ex)
	{
		bool isFail = false;

		if (ex is HttpRequestException httpRequestException && httpRequestException.InnerException is not null)
		{
			ex = httpRequestException.InnerException;
		}

		if (ex is TorConnectCommandFailedException torEx)
		{
			isFail = torEx.RepField == RepField.GeneralSocksServerFailure || torEx.RepField == RepField.OnionServiceIntroFailed;
		}

		if (isFail)
		{
			DoGenSocksServFail();
		}
		else
		{
			DoNotGenSocksServFail();
		}
	}

	private void DoGenSocksServFail()
	{
		InitialRequestTcs.TrySetResult(false);
		ResponseArrivedIsGenSocksServFail?.Invoke(this, true);
	}

	private void DoNotGenSocksServFail()
	{
		InitialRequestTcs.TrySetResult(true);
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

		Logger.LogTrace("<");
	}

	protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}
