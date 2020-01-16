using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Gui.Helpers;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class BuildTabViewModel : SendTabBaseViewModel
	{
		public override string DoButtonText => "Build Transaction";
		public override string DoingButtonText => "Building Transaction...";

		public BuildTabViewModel(WalletViewModel walletViewModel) : base(walletViewModel, "Build Transaction")
		{
		}

		protected override Task DoAfterBuildTransaction(BuildTransactionResult result)
		{
			try
			{
				var txviewer = IoC.Get<IShell>().Documents?.OfType<TransactionViewerViewModel>()?.FirstOrDefault(x => x.Wallet.Id == Wallet.Id);
				if (txviewer is null)
				{
					txviewer = new TransactionViewerViewModel(Wallet);
					IoC.Get<IShell>().AddDocument(txviewer);
				}
				IoC.Get<IShell>().Select(txviewer);

				txviewer.Update(result);

				ResetUi();

				NotificationHelpers.Success("Transaction is successfully built!", "");
			}
			catch (Exception ex)
			{
				return Task.FromException(ex);
			}
			return Task.CompletedTask;
		}
	}
}
