using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletLoadWorkflow
{
	private readonly CompositeDisposable _disposables = new();
	private readonly Wallet _wallet;
	private uint _lastestProcessBlockHeight;
	private Subject<(uint remainingFiltersToDownload, uint currentHeight, uint chainTip, double percent)> _progress;
	[AutoNotify] private bool _isLoading;
	public TaskCompletionSource<bool> InitialRequestTcs { get; } = new();

	public WalletLoadWorkflow(Wallet wallet)
	{
		_wallet = wallet;
		_progress = new();
		_progress.OnNext((0, 0, 0, 0));

		Services.EventBus.AsObservable<ServerTipHeightChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(e => true)
			.Take(1)
			.Subscribe(b => InitialRequestTcs.TrySetResult(b))
			.DisposeWith(_disposables);

		Services.EventBus.AsObservable<FilterProcessed>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.Filter.Header.Height)
			.Sample(TimeSpan.FromSeconds(1))
			.StartWith((uint)_wallet.KeyManager.GetBestHeight().Value)
			.Subscribe(x => _lastestProcessBlockHeight = x)
			.DisposeWith(_disposables);

		LoadCompleted = Services.EventBus.AsObservable<WalletLoaded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(x => x.Wallet == _wallet)
			.Select(x => x.Wallet.Loaded)
			.ToSignal();
	}

	public IObservable<(uint RemainingFiltersToDownload, uint CurrentHeight, uint ChainTip, double Percent)> Progress => _progress;

	public IObservable<Unit> LoadCompleted { get; }

	private uint InitialHeight { get; set; }
	private uint RemainingFiltersToDownload => (uint)Services.SmartHeaderChain.HashesLeft;

	public void Start()
	{
		Observable.FromAsync(() => InitialRequestTcs.Task)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Take(1)
			.SubscribeAsync(LoadWalletAsync)
			.DisposeWith(_disposables);

		Observable.Interval(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					_ =>
					{
						UpdateProgress();
					})
				.DisposeWith(_disposables);
	}

	public void Stop()
	{
		_disposables.Dispose();
	}

	private async Task LoadWalletAsync(bool isBackendAvailable)
	{
		IsLoading = true;

		await SetInitValuesAsync(isBackendAvailable).ConfigureAwait(false);

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
		if (isBackendAvailable)
		{
			// Wait until "server tip height" is initialized. It can be initialized only if Backend is available.
			await Services.SmartHeaderChain.ServerTipInitializedTcs.Task.ConfigureAwait(true);
		}

		// Wait until "client tip height" is initialized.
		await Services.BitcoinStore.FilterStore.InitializedTcs.Task.ConfigureAwait(true);

		InitialHeight = (uint) _wallet.KeyManager.GetBestHeight().Value;
	}

	private void UpdateProgress()
	{
		var serverTipHeight = Services.SmartHeaderChain.ServerTipHeight;
		var clientTipHeight = Services.SmartHeaderChain.TipHeight;

		var tipHeight = Math.Max(serverTipHeight, clientTipHeight);
		if (_lastestProcessBlockHeight == 0 || tipHeight == 0)
		{
			return;
		}

		var currentheight = _lastestProcessBlockHeight;
		var percentProgress = 100 * ((currentheight - InitialHeight) / (double)(tipHeight - InitialHeight));
		_progress.OnNext((RemainingFiltersToDownload, currentheight, tipHeight, percentProgress));
	}
}
