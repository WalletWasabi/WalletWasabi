using System.Linq;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinNodeFilterHelper
{
	public static Func<TreeNode, bool> FilterFunction<T>(string? text) where T : ICoin
	{
		return tn =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return true;
			}

			if (tn.Value is T coin)
			{
				if (coin.SmartLabel.IsEmpty)
				{
					var label = PrivacyLevelHelper.GetLabelFromPrivacyLevel(coin.PrivacyLevel);
					return label.Contains(text, StringComparison.InvariantCultureIgnoreCase);
				}

				var containsLabel = coin.SmartLabel.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
				return containsLabel;
			}

			return false;
		};
	}
}
