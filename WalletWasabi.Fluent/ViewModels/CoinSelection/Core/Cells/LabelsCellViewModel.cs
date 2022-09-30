using System.Collections;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class LabelsCellViewModel : ViewModelBase
{
	public LabelsCellViewModel(IEnumerable labels, PrivacyLevel privacyLevel = PrivacyLevel.None)
	{
		Labels = labels;
		PrivacyLevel = privacyLevel;

		if (PrivacyLevel == PrivacyLevel.None || PrivacyLevel == PrivacyLevel.NonPrivate)
		{
			IsNonPrivate = true;
		}
		else if (PrivacyLevel == PrivacyLevel.SemiPrivate)
		{
			IsSemiPrivate = true;
		}
		else
		{
			IsPrivate = true;
		}
	}

	public bool IsPrivate { get; }

	public bool IsSemiPrivate { get; }

	public bool IsNonPrivate { get; set; }

	public IEnumerable Labels { get; }

	public PrivacyLevel PrivacyLevel { get; }
}
