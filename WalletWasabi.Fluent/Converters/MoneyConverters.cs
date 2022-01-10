using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter MoneyToString =
		new FuncValueConverter<Money?, string>(x => x switch
		{
			null => "Unknown",
			{ } => x.ToString(fplus: false, trimExcessZero: true),
		});

	public static readonly IValueConverter ToFormattedString =
		new FuncValueConverter<Money?, string>(money => money is null ? "" : money.ToFormattedString());
}
