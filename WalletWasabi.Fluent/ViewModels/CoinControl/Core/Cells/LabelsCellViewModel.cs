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
			IsPrivate = true;
		}
		else if (coin.Labels == CoinPocketHelper.SemiPrivateFundsText)
		{
			IsSemiPrivate = true;
		}
		else
		{
			IsNonPrivate = true;
		}
	}

	public bool IsPrivate { get; }

	public bool IsSemiPrivate { get; }

	public bool IsNonPrivate { get; }

	public IEnumerable<string> Labels { get; }
}
