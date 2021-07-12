using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class LoadingViewModel : ActivatableViewModel
	{
		private readonly Wallet _wallet;

		[AutoNotify] private double _percent;
		[AutoNotify] private string? _statusText;
		[AutoNotify] private bool _isBackendConnected;

		private Stopwatch? _stopwatch;
		private bool _isLoading;
		private uint _filtersToDownloadCount;
		private uint _filtersToProcessCount;
		private uint _filterProcessStartingHeight;

		public LoadingViewModel(Wallet wallet)
		{
			_wallet = wallet;
			_statusText = "";
			_percent = 0;
			_isBackendConnected = Services.Synchronizer.BackendStatus == BackendStatus.Connected;
		}

		private uint TotalCount => _filtersToProcessCount + _filtersToDownloadCount;

		private uint RemainingFiltersToSync => (uint) Services.BitcoinStore.SmartHeaderChain.HashesLeft;

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_stopwatch ??= Stopwatch.StartNew();

			Observable.Interval(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					var downloadedFilters = _filtersToDownloadCount - RemainingFiltersToSync;

					uint processedFilters = 0;
					if (_wallet.LastProcessedFilter?.Header?.Height is { } lastProcessedFilterHeight)
					{
						processedFilters = lastProcessedFilterHeight - _filterProcessStartingHeight;
					}

					var processedCount = downloadedFilters + processedFilters;

					Console.WriteLine($"Total: {TotalCount}/{processedCount} Downloaded: {_filtersToDownloadCount}/{downloadedFilters} Processed: {_filtersToProcessCount}/{processedFilters}");

					UpdateStatus(processedCount, _stopwatch.ElapsedMilliseconds);
				})
				.DisposeWith(disposables);

			if (!_isLoading)
			{
				Services.Synchronizer.WhenAnyValue(x => x.BackendStatus)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(status => IsBackendConnected = status == BackendStatus.Connected)
					.DisposeWith(disposables);

				this.RaisePropertyChanged(nameof(IsBackendConnected));

				Observable.FromEventPattern<bool>(Services.Synchronizer, nameof(Services.Synchronizer.ResponseArrivedIsGenSocksServFail))
					.Subscribe(async _ =>
					{
						if (Services.Synchronizer.BackendStatus == BackendStatus.Connected) // TODO: the event invoke must be refactored in Synchronizer
						{
							return;
						}

						await LoadWalletAsync(syncFilters: false);
					})
					.DisposeWith(disposables);

				this.WhenAnyValue(x => x.IsBackendConnected)
					.Where(x => x)
					.Subscribe(async _ => await LoadWalletAsync(syncFilters: true))
					.DisposeWith(disposables);
			}
		}

		private async Task LoadWalletAsync(bool syncFilters)
		{
			if (_isLoading)
			{
				return;
			}

			_isLoading = true;

			SetInitValues();

			if (syncFilters)
			{
				while (RemainingFiltersToSync > 0)
				{
					await Task.Delay(1000);
				}
			}

			await UiServices.WalletManager.LoadWalletAsync(_wallet);
		}

		private void SetInitValues()
		{
			_filtersToDownloadCount = (uint) Services.BitcoinStore.SmartHeaderChain.HashesLeft;

			if (Services.BitcoinStore.SmartHeaderChain.ServerTipHeight is { } serverTipHeight &&
				Services.BitcoinStore.SmartHeaderChain.TipHeight is { } clientTipHeight)
			{
				var tipHeight = Math.Max(serverTipHeight, clientTipHeight);
				var startingHeight = SmartHeader.GetStartingHeader(_wallet.Network).Height;
				_filterProcessStartingHeight = (uint) _wallet.KeyManager.GetBestHeight().Value;

				_filtersToProcessCount = tipHeight - (_filterProcessStartingHeight < startingHeight ? startingHeight : _filterProcessStartingHeight);
			}
		}

		private void UpdateStatus(uint processedCount, double elapsedMilliseconds)
		{
			if (TotalCount == 0 || processedCount == 0)
			{
				return;
			}

			var percent = (decimal) processedCount / TotalCount * 100;
			var remainingCount = TotalCount - processedCount;
			var tempPercent = (uint) Math.Round(percent);

			if (tempPercent == 0 )
			{
				return;
			}

			Percent = tempPercent;
			var percentText = $"{Percent}% completed";

			var remainingMilliseconds = elapsedMilliseconds / processedCount * remainingCount;
			var userFriendlyTime = TextHelpers.TimeSpanToFriendlyString(TimeSpan.FromMilliseconds(remainingMilliseconds));
			var remainingTimeText = string.IsNullOrEmpty(userFriendlyTime) ? "" : $"- {userFriendlyTime} remaining";

			StatusText = $"{percentText} {remainingTimeText}";
		}
	}
}
