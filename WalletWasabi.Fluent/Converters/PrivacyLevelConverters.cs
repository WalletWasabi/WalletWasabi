using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WalletWasabi.Fluent.ViewModels.CoinSelection;

namespace WalletWasabi.Fluent.Converters;

public static class PrivacyLevelConverters
{
	public static readonly IValueConverter ToBrush =
		new FuncValueConverter<PrivacyLevel, IBrush>(pl =>
		{
			return pl switch
			{
				PrivacyLevel.SemiPrivate => Brushes.Yellow,
				PrivacyLevel.Private => Brushes.Green,
				PrivacyLevel.NonPrivate => Brushes.Red,
				_ => throw new ArgumentOutOfRangeException(nameof(pl), pl, null)
			};
		});
}
