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

public record SyncProgressCardModel(uint Initial, uint Current, uint Target)
{
	public double Percent => Target <= Initial
		? (Current >= Target ? 100 : 0)
		: Math.Clamp(100.0 * (Current - Initial) / (Target - Initial), 0, 100);

	public bool IsComplete => Percent >= 100;
}

public record WalletLoadProgress(
	uint ChainTip,
	SyncProgressCardModel Peers,
	SyncProgressCardModel BlockHeaders,
	SyncProgressCardModel FilterHeaders,
	SyncProgressCardModel CompactFilters,
	SyncProgressCardModel Blocks);

public partial class WalletLoadWorkflow
{
	private const uint TargetPeers = 12;

	private readonly IServices _services;
	private readonly CompositeDisposable _disposables = new();
	private readonly Wallet _wallet;
	private uint _blockHeadersTip;
	private uint _filterHeadersTip;
	private uint _compactFiltersTip;
	private uint _walletSyncHeight;
	private readonly Subject<WalletLoadProgress> _progress;
	[AutoNotify] private bool _isLoading;

	// Initial values for progress calculation
	private readonly uint _initialBlockHeaders;
	private readonly uint _initialWalletSyncHeight;
	private readonly uint _initialFilterHeaders;
	private readonly uint _initialCompactFilters;

	public WalletLoadWorkflow(IServices services, Wallet wallet)
	{
		_services = services;
		_wallet = wallet;
		_progress = new();

		var tipHeight = services.GetTipHeight();
		var blockHeadersTip = services.GetBlockHeadersTipHeight();
		var serverTipHeight = services.GetServerTipHeight();
		var walletBestHeight = (uint)wallet.KeyManager.GetBestHeight();

		// Store initial values for progress calculation
		_initialBlockHeaders = blockHeadersTip;
		_initialWalletSyncHeight = walletBestHeight;
		_initialFilterHeaders = tipHeight;
		_initialCompactFilters = tipHeight;

		_blockHeadersTip = blockHeadersTip;
		_filterHeadersTip = tipHeight;
		_compactFiltersTip = tipHeight;
		_walletSyncHeight = walletBestHeight;

		_progress.OnNext(new WalletLoadProgress(
			ChainTip: serverTipHeight,
			Peers: new SyncProgressCardModel(0, (uint)services.GetPeerCount(), TargetPeers),
			BlockHeaders: new SyncProgressCardModel(_initialBlockHeaders, _initialBlockHeaders, serverTipHeight),
			FilterHeaders: new SyncProgressCardModel(_initialFilterHeaders, _initialFilterHeaders, serverTipHeight),
			CompactFilters: new SyncProgressCardModel(_initialCompactFilters, _initialCompactFilters, serverTipHeight),
			Blocks: new SyncProgressCardModel(_initialWalletSyncHeight, _walletSyncHeight, serverTipHeight)));

		services.EventBus.AsObservable<BlockHeadersTipChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => _blockHeadersTip = x.Height)
			.DisposeWith(_disposables);

		services.EventBus.AsObservable<FilterHeadersTipChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => _filterHeadersTip = x.Height)
			.DisposeWith(_disposables);

		services.EventBus.AsObservable<ClientTipHeightChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x =>
			{
				_compactFiltersTip = x.Height;
				// Initially the filters' height is initialized but the filter headers' is not
				_filterHeadersTip = uint.Max(_filterHeadersTip, _compactFiltersTip);
			})
			.DisposeWith(_disposables);

		services.EventBus.AsObservable<BlockDownloaded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => _walletSyncHeight = x.Height)
			.DisposeWith(_disposables);

		LoadCompleted = services.EventBus.AsObservable<WalletLoaded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(x => x.Wallet == _wallet)
			.Select(x => x.Wallet.Loaded)
			.ToSignal();
	}

	public IObservable<WalletLoadProgress> Progress => _progress;

	public IObservable<Unit> LoadCompleted { get; }

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

		_progress.OnNext(new WalletLoadProgress(
			ChainTip: tipHeight,
			Peers: new SyncProgressCardModel(0, (uint)_services.GetPeerCount(), TargetPeers),
			BlockHeaders: new SyncProgressCardModel(_initialBlockHeaders, _blockHeadersTip, tipHeight),
			FilterHeaders: new SyncProgressCardModel(_initialFilterHeaders, _filterHeadersTip, tipHeight),
			CompactFilters: new SyncProgressCardModel(_initialCompactFilters, _compactFiltersTip, tipHeight),
			Blocks: new SyncProgressCardModel(_initialWalletSyncHeight, _walletSyncHeight, tipHeight)));
	}
}
