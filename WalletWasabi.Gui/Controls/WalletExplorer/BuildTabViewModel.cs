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
		
		private WalletService WalletService { get; }

		public BuildTabViewModel(WalletService walletService) : base(walletService, "Build Transaction")
		{
			WalletService = walletService;
		}

		protected override Task DoAfterBuildTransaction(BuildTransactionResult result)
		{
			try
			{
				var txviewer = IoC.Get<IShell>().Documents?.OfType<TransactionViewerViewModel>()?.FirstOrDefault(x => x.WalletService.CompareTo(WalletService) == 0);
				if (txviewer is null)
				{
					txviewer = new TransactionViewerViewModel(WalletService);
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
