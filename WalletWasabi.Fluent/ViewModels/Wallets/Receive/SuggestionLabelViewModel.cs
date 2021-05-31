using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class SuggestionLabelViewModel : ViewModelBase
	{
		public SuggestionLabelViewModel(string suggestion, ICommand selectedCommand)
		{
			Suggestion = suggestion;
			SelectedCommand = selectedCommand;
		}

		public string Suggestion { get; }

		public ICommand SelectedCommand { get; }
	}
}