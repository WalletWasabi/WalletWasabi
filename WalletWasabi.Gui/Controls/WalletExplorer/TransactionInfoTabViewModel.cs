using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public TransactionInfoTabViewModel(TransactionViewModel transaction) : base(string.Empty)
		{
			Transaction = transaction;
			Title = $"Transaction ({transaction.TransactionId[0..10]}) Details";
		}

		public TransactionViewModel Transaction { get; }
	}
}