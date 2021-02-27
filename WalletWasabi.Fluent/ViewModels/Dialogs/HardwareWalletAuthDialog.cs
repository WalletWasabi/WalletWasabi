using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Enter your password")]
	public partial class HardwareWalletAuthDialog : DialogViewModelBase<bool>
	{
		public HardwareWalletAuthDialog(Wallet wallet, ref BuildTransactionResult bts)
		{
			var transaction = bts;

			var canExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back), canExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel), canExecute);
			NextCommand = ReactiveCommand.CreateFromTask(async () => Close(DialogResultKind.Normal, true), canExecute);

			EnableAutoBusyOn(NextCommand);
		}
	}
}
