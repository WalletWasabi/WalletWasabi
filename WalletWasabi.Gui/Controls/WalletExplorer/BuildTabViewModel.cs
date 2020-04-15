using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class BuildTabViewModel : SendControlViewModel
	{
		public BuildTabViewModel(Wallet wallet) 
			: base(wallet, "Build Transaction", false)
		{
		}

		public override string DoButtonText => "Build Transaction";
		public override string DoingButtonText => "Building Transaction...";

		protected override Task DoAfterBuildTransaction(BuildTransactionResult result)
		{
			try
			{
				var txviewer = new TransactionViewerViewModel();
				IoC.Get<IShell>().AddDocument(txviewer);
				IoC.Get<IShell>().Select(txviewer);

				txviewer.Update(result);

				ResetUi();

				NotificationHelpers.Success("Transaction was built.");
			}
			catch (Exception ex)
			{
				return Task.FromException(ex);
			}
			return Task.CompletedTask;
		}
	}
}
