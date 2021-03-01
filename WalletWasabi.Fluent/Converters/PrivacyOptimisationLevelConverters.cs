using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Converters
{
	public static class PrivacyOptimisationLevelConverters
	{
		public static readonly IValueConverter OptimisationLevelToBrushConverter =
			new FuncValueConverter<PrivacyOptimisationLevel, IBrush?>(x =>
			{
				var resourceName = x == PrivacyOptimisationLevel.Standard
					? "PrivacyOptimisationLevelStandardBrush"
					: "PrivacyOptimisationLevelBetterBrush";

				if (Application.Current.Styles.TryGetResource(resourceName, out var resource) &&
				    resource is IBrush brush)
				{
					return brush;
				}

				return null;
			});
	}
}