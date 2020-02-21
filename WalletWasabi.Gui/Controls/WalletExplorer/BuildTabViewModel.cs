using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class BuildTabViewModel : SendControlViewModel
	{
		public override string DoButtonText => "Build Transaction";
		public override string DoingButtonText => "Building Transaction...";

		private Guid _walletId;

		public BuildTabViewModel(WalletService walletService, Guid walletId) : base(walletService, "Build Transaction")
		{
			_walletId = walletId;
		}

		protected override Task DoAfterBuildTransaction(BuildTransactionResult result)
		{
			try
			{
				var txviewer = IoC.Get<IShell>().Documents?.OfType<TransactionViewerViewModel>()?.FirstOrDefault(x => x.Id == _walletId);
				if (txviewer is null)
				{
					txviewer = new TransactionViewerViewModel(_walletId);
					IoC.Get<IShell>().AddDocument(txviewer);
				}
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
