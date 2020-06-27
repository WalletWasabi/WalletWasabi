using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public TransactionInfoTabViewModel(TransactionInfo transaction) : base("")
		{
			Transaction = transaction;
			Title = $"Transaction ({transaction.TransactionId[0..10]}) Details";
		}

		public TransactionInfo Transaction { get; }
	}
}
