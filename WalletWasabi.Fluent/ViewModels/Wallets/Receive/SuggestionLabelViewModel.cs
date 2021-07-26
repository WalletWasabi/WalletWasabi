namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class SuggestionLabelViewModel : ViewModelBase
	{
		public SuggestionLabelViewModel(string label, int count)
		{
			Label = label;
			Count = count;
		}

		public string Label { get; }

		public int Count { get; }
	}
}