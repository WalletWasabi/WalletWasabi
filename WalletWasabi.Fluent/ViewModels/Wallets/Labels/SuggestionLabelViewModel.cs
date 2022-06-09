namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels;

public class SuggestionLabelViewModel : ViewModelBase
{
	public SuggestionLabelViewModel(string label, int score)
	{
		Label = label;
		Score = score;
	}

	public string Label { get; }

	public int Score { get; }
}
