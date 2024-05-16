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
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class WasabiSynchronizer : PeriodicRunner, INotifyPropertyChanged, IWasabiBackendStatusProvider
{
	private decimal _usdExchangeRate;

	private TorStatus _torStatus;

	private BackendStatus _backendStatus;

	public WasabiSynchronizer(TimeSpan period, int maxFiltersToSync, BitcoinStore bitcoinStore, WasabiHttpClientFactory httpClientFactory) : base(period)
	{
		MaxFiltersToSync = maxFiltersToSync;

		LastResponse = null;
		SmartHeaderChain = bitcoinStore.SmartHeaderChain;
		FilterProcessor = new FilterProcessor(bitcoinStore);
		HttpClientFactory = httpClientFactory;
		WasabiClient = httpClientFactory.SharedWasabiClient;
	}

	#region EventsPropertiesMembers

	public event EventHandler<bool>? SynchronizeRequestFinished;

	public event EventHandler<SynchronizeResponse>? ResponseArrived;

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Task completion source that is completed once a first synchronization request succeeds or fails.</summary>
	public TaskCompletionSource<bool> InitialRequestTcs { get; } = new();

	public SynchronizeResponse? LastResponse { get; private set; }
	public WasabiHttpClientFactory HttpClientFactory { get; }
	private WasabiClient WasabiClient { get; }

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
	private int MaxFiltersToSync { get; }
	private SmartHeaderChain SmartHeaderChain { get; }
	private FilterProcessor FilterProcessor { get; }

	public bool InError => BackendStatus != BackendStatus.Connected;

	#endregion EventsPropertiesMembers

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		try
		{
			SynchronizeResponse response;

			ushort lastUsedApiVersion = WasabiClient.ApiVersion;
			try
			{
				if (SmartHeaderChain.TipHash is null)
				{
					return;
				}

				response = await WasabiClient
					.GetSynchronizeAsync(SmartHeaderChain.TipHash, MaxFiltersToSync, EstimateSmartFeeMode.Conservative, cancel)
					.ConfigureAwait(false);

				// NOT GenSocksServErr
				BackendStatus = BackendStatus.Connected;
				TorStatus = TorStatus.Running;
				OnSynchronizeRequestFinished();
			}
			catch (HttpRequestException ex) when (ex.InnerException is TorException innerEx)
			{
				TorStatus = innerEx is TorConnectionException ? TorStatus.NotRunning : TorStatus.Running;
				BackendStatus = BackendStatus.NotConnected;
				OnSynchronizeRequestFinished();
				throw;
			}
			catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
			{
				TorStatus = TorStatus.Running;
				BackendStatus = BackendStatus.NotConnected;

				// Backend API version might be updated meanwhile. Trying to update the versions.
				var result = await WasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);

				// If the backend is compatible and the Api version updated then we just used the wrong API.
				if (result.BackendCompatible && lastUsedApiVersion != WasabiClient.ApiVersion)
				{
					// Next request will be fine, do not throw exception.
					TriggerRound();
					return;
				}
				else
				{
					throw;
				}
			}
			catch (Exception)
			{
				TorStatus = TorStatus.Running;
				BackendStatus = BackendStatus.NotConnected;
				OnSynchronizeRequestFinished();
				throw;
			}

			// If it's not fully synced or reorg happened.
			if (response.Filters.Count() == MaxFiltersToSync || response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
			{
				TriggerRound();
			}

			await FilterProcessor.ProcessAsync((uint)response.BestHeight, response.FiltersResponseState, response.Filters).ConfigureAwait(false);

			LastResponse = response;
			ResponseArrived?.Invoke(this, response);
		}
		catch (HttpRequestException)
		{
			await Task.Delay(3000, cancel).ConfigureAwait(false); // Retry sooner in case of connection error.
			TriggerRound();
			throw;
		}
	}

	private void OnSynchronizeRequestFinished()
	{
		var isBackendConnected = BackendStatus is BackendStatus.Connected;

		// One time trigger for the UI about the first request.
		InitialRequestTcs.TrySetResult(isBackendConnected);

		SynchronizeRequestFinished?.Invoke(this, isBackendConnected);
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
