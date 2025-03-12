using NBitcoin.RPC;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class WasabiSynchronizer(
	TimeSpan period,
	int maxFiltersToSync,
	BitcoinStore bitcoinStore,
	IHttpClientFactory httpClientFactory)
	: PeriodicRunner(period), INotifyPropertyChanged, IWasabiBackendStatusProvider
{
	private TorStatus _torStatus;
	private BackendStatus _backendStatus;
	private bool _backendNotCompatible;
	private readonly SmartHeaderChain _smartHeaderChain = bitcoinStore.SmartHeaderChain;
	private readonly FilterProcessor _filterProcessor = new(bitcoinStore);
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("long-live-satoshi-backend");

	#region EventsPropertiesMembers

	public event EventHandler<bool>? SynchronizeRequestFinished;

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Task completion source that is completed once a first synchronization request succeeds or fails.</summary>
	public TaskCompletionSource<bool> InitialRequestTcs { get; } = new();

	public SynchronizeResponse? LastResponse { get; private set; } = null;

	public TorStatus TorStatus
	{
		get => _torStatus;
		private set => RaiseAndSetIfChanged(ref _torStatus, value);
	}

	public BackendStatus BackendStatus
	{
		get => _backendStatus;
		private set => RaiseAndSetIfChanged(ref _backendStatus, value);
	}

	public bool BackendNotCompatible
	{
		get => _backendNotCompatible;
		private set => RaiseAndSetIfChanged(ref _backendNotCompatible, value);
	}


	#endregion EventsPropertiesMembers

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var wasabiClient = new WasabiClient(_httpClient);
		var lastUsedApiVersion = WasabiClient.ApiVersion;
		if (_smartHeaderChain.TipHash is null)
		{
			return;
		}

		try
		{
			var response = await wasabiClient
				.GetSynchronizeAsync(_smartHeaderChain.TipHash, maxFiltersToSync, EstimateSmartFeeMode.Conservative, cancel)
				.ConfigureAwait(false);

			UpdateStatus(BackendStatus.Connected, TorStatus.Running, false);
			OnSynchronizeRequestFinished();

			// If it's not fully synced or reorg happened.
			if (response.Filters.Count() == maxFiltersToSync || response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
			{
				TriggerRound();
			}

			await _filterProcessor.ProcessAsync((uint)response.BestHeight, response.FiltersResponseState, response.Filters).ConfigureAwait(false);

			LastResponse = response;
		}
		catch (HttpRequestException ex) when (ex.InnerException is SocketException innerEx)
		{
			UpdateStatus(
				BackendStatus.NotConnected,
				innerEx.SocketErrorCode == SocketError.ConnectionRefused
					? TorStatus.NotRunning
					: TorStatus.Running,
				false);
			OnSynchronizeRequestFinished();

			await Task.Delay(3000, cancel).ConfigureAwait(false); // Retry sooner in case of connection error.
			TriggerRound();
			throw;
		}
		catch (HttpRequestException ex) when (ex.Message.Contains("Not Found"))
		{
			TorStatus = TorStatus.Running;
			BackendStatus = BackendStatus.NotConnected;

			// Backend API version might be updated meanwhile. Trying to update the versions.
			bool backendCompatible;
			try
			{
				backendCompatible = await wasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
			}
			catch (HttpRequestException) when (ex.Message.Contains("Not Found"))
			{
				// Backend is online but the endpoint for versions doesn't exist -> backend is not compatible.
				backendCompatible = false;
			}

			UpdateStatus( BackendStatus.NotConnected, TorStatus.Running, !backendCompatible);

			// If the backend is compatible and the Api version updated then we just used the wrong API.
			if (backendCompatible && lastUsedApiVersion != WasabiClient.ApiVersion)
			{
				// Next request will be fine, do not throw exception.
				TriggerRound();
				return;
			}

			await Task.Delay(3000, cancel).ConfigureAwait(false); // Retry sooner in case of connection error.
			TriggerRound();
			throw;
		}
		catch (Exception)
		{
			UpdateStatus(BackendStatus.NotConnected, TorStatus.Running, false);
			OnSynchronizeRequestFinished();
			throw;
		}
	}

	private void UpdateStatus(BackendStatus backendStatus, TorStatus torStatus, bool backendNotCompatible)
	{
		BackendStatus = backendStatus;
		TorStatus = torStatus;
		BackendNotCompatible = backendNotCompatible;
	}

	private void OnSynchronizeRequestFinished()
	{
		var isBackendConnected = BackendStatus is BackendStatus.Connected;

		// One time trigger for the UI about the first request.
		InitialRequestTcs.TrySetResult(isBackendConnected);

		SynchronizeRequestFinished?.Invoke(this, isBackendConnected);
	}

	private void RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
