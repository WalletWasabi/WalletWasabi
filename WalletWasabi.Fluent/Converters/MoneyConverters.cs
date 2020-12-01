using Avalonia.Data.Converters;
using Avalonia.Media;
using NBitcoin;

namespace WalletWasabi.Fluent.Converters
{
	public static class MoneyConverters
	{
		public static readonly IValueConverter MoneyToString =
			new FuncValueConverter<Money, string>(x =>
			{
				return x switch
				{
					null => "Unknown",
					{ } => x.ToString(fplus: false, trimExcessZero: true),
				};
			});

		public static readonly IValueConverter MoneyToBrush =
			new FuncValueConverter<Money, ISolidColorBrush>(x => x.ToDecimal(MoneyUnit.BTC) < 0 ? Brushes.IndianRed : Brushes.MediumSeaGreen);
	}
}