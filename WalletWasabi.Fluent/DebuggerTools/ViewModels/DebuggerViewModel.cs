
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebuggerViewModel : ViewModelBase
{
	[AutoNotify] private DebugWalletViewModel? _selectedWallet;

	public DebuggerViewModel()
	{
		var wallets =
			Services.WalletManager
				.GetWallets()
				.Select(x => new DebugWalletViewModel(x));

		Wallets = new ObservableCollection<DebugWalletViewModel>(wallets);

		SelectedWallet = Wallets.FirstOrDefault();

		Observable
			.FromEventPattern<WalletState>(Services.WalletManager, nameof(WalletManager.WalletStateChanged))
			.Select(x => x.Sender as Wallet)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(wallet =>
			{
				// TODO:
			});

		Observable
			.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(wallet =>
			{
				Wallets.Add(new DebugWalletViewModel(wallet));
			});

		Observable
			.FromEventPattern<ProcessedResult>(Services.WalletManager, nameof(WalletManager.WalletRelevantTransactionProcessed))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(async arg =>
			{
				// TODO:
			});
	}

	public ObservableCollection<DebugWalletViewModel> Wallets { get; }
}
