using Avalonia.Data.Converters;
using NBitcoin;

namespace WalletWasabi.Fluent.Converters
{
	public static class MoneyConverters
	{
		public static readonly IValueConverter MoneyToString =
			new FuncValueConverter<Money?, string>(x =>
			{
				return x switch
				{
					null => "Unknown",
					{ } => x.ToString(fplus: false, trimExcessZero: true),
				};
			});
	}
}