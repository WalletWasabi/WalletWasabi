namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels;

public class SuggestionLabelViewModel : ViewModelBase
{
	public SuggestionLabelViewModel(UiContext uiContext, string label, int score) : base(uiContext)
	{
		Label = label;
		Score = score;
	}

	public string Label { get; }

	public int Score { get; }
}
