using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletLoadWorkflow
{
	private readonly IServices _services;
	private readonly CompositeDisposable _disposables = new();
	private readonly Wallet _wallet;
	private uint _latestProcessBlockHeight;
	private Subject<(uint remainingFiltersToDownload, uint currentHeight, uint chainTip, double percent)> _progress;
	[AutoNotify] private bool _isLoading;

	public WalletLoadWorkflow(IServices services, Wallet wallet)
	{
		_services = services;
		_wallet = wallet;
		_progress = new();
		_progress.OnNext((0, 0, 0, 0));

		services.EventBus.AsObservable<FilterProcessed>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.Filter.Header.Height)
			.Sample(TimeSpan.FromSeconds(1))
			.StartWith(_wallet.KeyManager.GetBestHeight())
			.Subscribe(x => _latestProcessBlockHeight = x)
			.DisposeWith(_disposables);

		LoadCompleted = services.EventBus.AsObservable<WalletLoaded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(x => x.Wallet == _wallet)
			.Select(x => x.Wallet.Loaded)
			.ToSignal();
	}

	public IObservable<(uint RemainingFiltersToDownload, uint CurrentHeight, uint ChainTip, double Percent)> Progress => _progress;

	public IObservable<Unit> LoadCompleted { get; }

	private uint InitialHeight { get; set; }
	private uint RemainingFiltersToDownload => (uint)_services.GetHashesLeft();

	public void Start()
	{
		Observable.FromAsync(() => Task.CompletedTask)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Take(1)
			.SubscribeAsync(_ => LoadWalletAsync())
			.DisposeWith(_disposables);

		Observable.Interval(TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => UpdateProgress())
			.DisposeWith(_disposables);
	}

	public void Stop()
	{
		_disposables.Dispose();
	}

	private async Task LoadWalletAsync()
	{
		IsLoading = true;
		InitialHeight = _wallet.KeyManager.GetBestHeight().Height;

		await WaitForHeightsAsync().ConfigureAwait(false);

		try
		{
			await Task.Run(async () => await _services.StartWalletAsync(_wallet));
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

	private async Task WaitForHeightsAsync()
	{
		// Wait until "server tip height" is initialized.
		var waitForNetworkHeight = _services.EventBus.WaitForEventAsync<NetworkTipHeightChanged>(
			() => _services.GetServerTipHeight() > 0);

		// Wait until "client tip height" is initialized.
		var waitForClientHeight = _services.EventBus.WaitForEventAsync<ClientTipHeightChanged>(
			() => _services.GetTip() is not null);

		await Task.WhenAll(waitForNetworkHeight, waitForClientHeight).ConfigureAwait(false);
	}

	private void UpdateProgress()
	{
		var serverTipHeight = _services.GetServerTipHeight();
		var clientTipHeight = _services.GetTipHeight();

		var tipHeight = Math.Max(serverTipHeight, clientTipHeight);
		if (_latestProcessBlockHeight == 0 || tipHeight == 0)
		{
			return;
		}

		var currentHeight = _latestProcessBlockHeight;
		var percentProgress = 100 * ((currentHeight - InitialHeight) / (double)(tipHeight - InitialHeight));
		_progress.OnNext((RemainingFiltersToDownload, currentHeight, tipHeight, percentProgress));
	}
}
