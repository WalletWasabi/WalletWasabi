namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels;

public class SuggestionLabelViewModel : ViewModelBase
{
	public SuggestionLabelViewModel(string label, int count)
	{
		Label = label;
		Count = count;
	}

	public string Label { get; }

	public int Count { get; }
}
