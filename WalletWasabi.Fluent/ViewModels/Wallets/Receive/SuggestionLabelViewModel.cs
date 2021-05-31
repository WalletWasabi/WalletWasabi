using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class SuggestionLabelViewModel : ViewModelBase
	{
		public SuggestionLabelViewModel(string label, int count, ICommand selectedCommand)
		{
			Label = label;
			Count = count;
			SelectedCommand = selectedCommand;
		}

		public string Label { get; }

		public int Count { get; }

		public ICommand SelectedCommand { get; }
	}
}