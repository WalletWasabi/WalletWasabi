using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = null)]
public partial class LoadingViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;

	[AutoNotify] private double _percent;
	[AutoNotify] private string _statusText = " "; // Should not be empty as we have to preserve the space in the view.
	[AutoNotify] private bool _isLoading;

	private Stopwatch? _stopwatch;
	private uint _filtersToDownloadCount;
	private uint _filtersToProcessCount;
	private uint _filterProcessStartingHeight;
	private CompositeDisposable? _disposable;

	public LoadingViewModel(Wallet wallet)
	{
		_wallet = wallet;
		_percent = 0;
	}

	public string WalletName => _wallet.WalletName;

	private uint TotalCount => _filtersToProcessCount + _filtersToDownloadCount;

	private uint RemainingFiltersToDownload => (uint)Services.BitcoinStore.SmartHeaderChain.HashesLeft;

	// Workaround for: https://github.com/zkSNACKs/WalletWasabi/pull/10576#discussion_r1209973481
	// Remove in next PR
	public void Activate()
	{
		_disposable = new CompositeDisposable();

		_stopwatch = Stopwatch.StartNew();

		_disposable.Add(Disposable.Create(() => _stopwatch.Stop()));

		Services.Synchronizer.WhenAnyValue(x => x.BackendStatus)
			.Where(status => status == BackendStatus.Connected)
			.SubscribeAsync(async _ => await LoadWalletAsync(isBackendAvailable: true).ConfigureAwait(false))
			.DisposeWith(_disposable);

		Observable.FromEventPattern<bool>(Services.Synchronizer, nameof(Services.Synchronizer.ResponseArrivedIsGenSocksServFail))
			.SubscribeAsync(async _ =>
			{
				if (Services.Synchronizer.BackendStatus == BackendStatus.Connected)
				{
					return;
				}

				await LoadWalletAsync(isBackendAvailable: false).ConfigureAwait(false);
			})
			.DisposeWith(_disposable);

		Observable.Interval(TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				var processedCount = GetCurrentProcessedCount();
				UpdateStatus(processedCount);
			})
			.DisposeWith(_disposable);
	}

	// Workaround for: https://github.com/zkSNACKs/WalletWasabi/pull/10576#discussion_r1209973481
	// Remove in next PR
	public void Deactivate()
	{
		_disposable?.Dispose();
	}

	private uint GetCurrentProcessedCount()
	{
		uint downloadedFilters = 0;
		if (_filtersToDownloadCount > 0)
		{
			downloadedFilters = _filtersToDownloadCount - RemainingFiltersToDownload;
		}

		uint processedFilters = 0;
		if (_wallet.LastProcessedFilter?.Header?.Height is { } lastProcessedFilterHeight)
		{
			processedFilters = lastProcessedFilterHeight - _filterProcessStartingHeight - 1;
		}

		var processedCount = downloadedFilters + processedFilters;

		return processedCount;
	}

	private async Task LoadWalletAsync(bool isBackendAvailable)
	{
		if (IsLoading)
		{
			return;
		}

		IsLoading = true;

		await SetInitValuesAsync(isBackendAvailable).ConfigureAwait(false);

		while (isBackendAvailable && RemainingFiltersToDownload > 0 && !_wallet.KeyManager.SkipSynchronization)
		{
			await Task.Delay(1000).ConfigureAwait(false);
		}

		if (_wallet.State != WalletState.Uninitialized)
		{
			throw new Exception("Wallet is already being logged in.");
		}

		try
		{
			await Task.Run(async () => await Services.WalletManager.StartWalletAsync(_wallet));
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogTrace(ex);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private async Task SetInitValuesAsync(bool isBackendAvailable)
	{
		while (isBackendAvailable && Services.Synchronizer.LastResponse is null)
		{
			await Task.Delay(500).ConfigureAwait(false);
		}

		_filtersToDownloadCount = (uint)Services.BitcoinStore.SmartHeaderChain.HashesLeft;

		if (Services.BitcoinStore.SmartHeaderChain.ServerTipHeight is { } serverTipHeight &&
			Services.BitcoinStore.SmartHeaderChain.TipHeight is { } clientTipHeight)
		{
			var tipHeight = Math.Max(serverTipHeight, clientTipHeight);
			var startingHeight = SmartHeader.GetStartingHeader(_wallet.Network, IndexType.SegwitTaproot).Height;
			var bestHeight = (uint)_wallet.KeyManager.GetBestHeight().Value;
			_filterProcessStartingHeight = bestHeight < startingHeight ? startingHeight : bestHeight;

			_filtersToProcessCount = tipHeight - _filterProcessStartingHeight;
		}
	}

	private void UpdateStatus(uint processedCount)
	{
		if (TotalCount == 0 || processedCount == 0 || processedCount > TotalCount || _stopwatch is null)
		{
			return;
		}

		var percent = (decimal)processedCount / TotalCount * 100;
		var remainingCount = TotalCount - processedCount;
		var tempPercent = (uint)Math.Round(percent);

		if (tempPercent == 0)
		{
			return;
		}

		Percent = tempPercent;
		var percentText = $"{Percent}% completed";

		var remainingMilliseconds = (double)_stopwatch.ElapsedMilliseconds / processedCount * remainingCount;
		var remainingTimeSpan = TimeSpan.FromMilliseconds(remainingMilliseconds);

		if (remainingTimeSpan > TimeSpan.FromHours(1))
		{
			remainingTimeSpan = new TimeSpan(remainingTimeSpan.Days, remainingTimeSpan.Hours, remainingTimeSpan.Minutes, seconds: 0);
		}

		var userFriendlyTime = TextHelpers.TimeSpanToFriendlyString(remainingTimeSpan);
		var remainingTimeText = string.IsNullOrEmpty(userFriendlyTime) ? "" : $"- {userFriendlyTime} remaining";

		StatusText = $"{percentText} {remainingTimeText}";
	}
}
