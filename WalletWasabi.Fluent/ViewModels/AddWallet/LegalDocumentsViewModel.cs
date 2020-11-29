using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class LegalDocumentsViewModel : RoutableViewModel
	{
		public LegalDocumentsViewModel(string content)
		{
			Content = content;

			NextCommand = BackCommand;
		}

		public string Content { get; }
	}
}