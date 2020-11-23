using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class LegalDocumentsViewModel : RoutableViewModel
	{
		public LegalDocumentsViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, string content) :
			base(navigationState, navigationTarget)
		{
			Content = content;
		}

		public ICommand NextCommand => BackCommand;

		public string Content { get; }
	}
}