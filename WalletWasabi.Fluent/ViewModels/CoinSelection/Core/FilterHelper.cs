using System.Linq;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public class FilterHelper
{
	public static Func<TreeNode, bool> FilterFunction<T>(string? text) where T : ICoin
	{
		return tn =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return true;
			}

			if (tn.Value is T cg)
			{
				var containsLabel = cg.SmartLabel.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
				return containsLabel;
			}

			return false;
		};
	}
}
