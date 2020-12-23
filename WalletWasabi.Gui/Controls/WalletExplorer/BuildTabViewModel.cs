using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class BuildTabViewModel : SendControlViewModel
	{
		public BuildTabViewModel(Wallet wallet) : base(wallet, "Build Transaction")
		{
		}

		public override string DoButtonText => "Build Transaction";
		public override string DoingButtonText => "Building Transaction...";

		// Must be here, it is bound at SendControlView.xaml.
		public string PayjoinEndPoint => null;

		protected override async Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy, bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null)
		{
			BuildTransactionResult result = await Task.Run(() => Wallet.BuildTransaction(Password, payments, feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs));

			var txviewer = new TransactionViewerViewModel();
			IoC.Get<IShell>().AddDocument(txviewer);
			IoC.Get<IShell>().Select(txviewer);

			txviewer.Update(result);
			ResetUi();
			NotificationHelpers.Success("Transaction was built.");
		}
	}
}
