using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

public static class CoinControlLabelComparer
{
	public static int Ascending(CoinControlItemViewModelBase? left, CoinControlItemViewModelBase? right)
	{
		if (left is null)
		{
			return right is null ? 0 : -1;
		}

		if (right is null)
		{
			return 1;
		}

		var privateScoreLeft = GetLabelPriority(left);
		var privateScoreRight = GetLabelPriority(right);

		if (privateScoreLeft == privateScoreRight)
		{
			return StringComparer.InvariantCultureIgnoreCase.Compare(left.Labels, right.Labels);
		}

		return privateScoreLeft > privateScoreRight ? -1 : 1;
	}

	public static int Descending(CoinControlItemViewModelBase? left, CoinControlItemViewModelBase? right)
	{
		return -Ascending(left, right);
	}

	private static int GetLabelPriority(CoinControlItemViewModelBase coin)
	{
		if (coin.Labels == CoinPocketHelper.PrivateFundsText)
		{
			return 3;
		}

		if (coin.Labels == CoinPocketHelper.SemiPrivateFundsText)
		{
			return 2;
		}

		return 1;
	}
}
