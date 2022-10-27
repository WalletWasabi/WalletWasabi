using System.Collections.Generic;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core.Cells;

public class LabelsCellViewModel : ViewModelBase
{
	public LabelsCellViewModel(ICoin coin)
	{
		Labels = coin.Labels;

		if (coin.Labels == CoinPocketHelper.PrivateFundsText)
		{
			PrivacyLevel = Privacy.Private;
		}
		else if (coin.Labels == CoinPocketHelper.SemiPrivateFundsText)
		{
			PrivacyLevel = Privacy.SemiPrivate;
		}
		else
		{
			PrivacyLevel = Privacy.NonPrivate;
		}
	}

	private enum Privacy
	{
		Invalid = 0,
		SemiPrivate,
		Private,
		NonPrivate
	}

	public bool IsPrivate => PrivacyLevel == Privacy.Private;

	public bool IsSemiPrivate => PrivacyLevel == Privacy.SemiPrivate;

	public bool IsNonPrivate => PrivacyLevel == Privacy.NonPrivate;

	public IEnumerable<string> Labels { get; }

	private Privacy PrivacyLevel { get; }
}
