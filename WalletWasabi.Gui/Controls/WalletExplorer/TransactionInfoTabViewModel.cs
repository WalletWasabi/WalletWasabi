using WalletWasabi.Gui.Controls.TransactionDetails;
using WalletWasabi.Gui.Controls.TransactionDetails.ViewModels;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public TransactionInfoTabViewModel(TransactionDetailsViewModel transaction) : base("")
		{
			Transaction = transaction;
			Title = $"Transaction ({transaction.TransactionId[0..10]}) Details";
		}

		public TransactionDetailsViewModel Transaction { get; }
	}
}
