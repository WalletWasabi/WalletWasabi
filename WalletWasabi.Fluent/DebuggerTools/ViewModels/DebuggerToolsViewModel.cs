using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebuggerToolsViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposable;
	private bool _isInitialized;
	[AutoNotify] private DebugWalletViewModel? _selectedWallet;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugWalletViewModel>? _wallets;

	public DebuggerToolsViewModel()
	{
		_disposable = new CompositeDisposable();
	}

	public void Initialize()
	{
		if (_isInitialized)
		{
			throw new InvalidOperationException();
		}

		Observable
			.FromEventPattern<WalletState>(Services.WalletManager, nameof(WalletManager.WalletStateChanged))
			.Select(x => x.Sender as Wallet)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(wallet =>
			{
				// TODO:
			})
			.DisposeWith(_disposable);

		Observable
			.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(wallet =>
			{
				Wallets?.Add(new DebugWalletViewModel(wallet));
			})
			.DisposeWith(_disposable);

		Observable
			.FromEventPattern<ProcessedResult>(Services.WalletManager, nameof(WalletManager.WalletRelevantTransactionProcessed))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(async arg =>
			{
				// TODO:
			})
			.DisposeWith(_disposable);

		var wallets =
			Services.WalletManager
				.GetWallets()
				.Select(x => new DebugWalletViewModel(x));

		Wallets = new ObservableCollection<DebugWalletViewModel>(wallets);

		SelectedWallet = Wallets.FirstOrDefault();

		_isInitialized = true;
	}

	public void Dispose()
	{
		_disposable.Dispose();

		if (_wallets is { })
		{
			foreach (var wallet in _wallets)
			{
				wallet.Dispose();
			}
		}
	}
}
