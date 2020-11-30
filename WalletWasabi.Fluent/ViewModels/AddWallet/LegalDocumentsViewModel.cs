using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class LegalDocumentsViewModel : RoutableViewModel
	{
		public LegalDocumentsViewModel(string content, bool isNextEnabled)
		{
			Content = content;

			NextCommand = BackCommand;

			IsNextEnabled = isNextEnabled;
		}

		public override NavigationTarget DefaultTarget => NavigationTarget.DialogScreen;

		public string Content { get; }

		public bool IsNextEnabled { get; }
	}
}