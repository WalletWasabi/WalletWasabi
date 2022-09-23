namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class AnonymityScoreCellViewModel : ViewModelBase
{
	public AnonymityScoreCellViewModel(SelectableCoin coin)
	{
		PrivacyScore = coin.AnonymitySet;
		PrivacyLevel = coin.PrivacyLevel;
		IsPrivate = PrivacyLevel == PrivacyLevel.Private;
		IsSemiPrivate = PrivacyLevel == PrivacyLevel.SemiPrivate;
		IsNonPrivate = PrivacyLevel == PrivacyLevel.NonPrivate;
	}

	public bool IsSemiPrivate { get; }

	public bool IsNonPrivate { get; }

	public bool IsPrivate { get; }

	public PrivacyLevel PrivacyLevel { get; }

	public int PrivacyScore { get; }
}
