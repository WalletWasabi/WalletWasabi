using System;
using Avalonia.Data.Converters;
using NBitcoin;

namespace WalletWasabi.Fluent.Converters
{
	public static class MoneyConverters
	{
		public static readonly IValueConverter MoneyToString =
			new FuncValueConverter<Money?, string>(x => x switch
			{
				null => "Unknown",
				{ } => x.ToString(fplus: false, trimExcessZero: true),
			});

		public static readonly IValueConverter ToFormattedString =
			new FuncValueConverter<Money?, string>(money =>
			{
				if (money is null)
				{
					return "";
				}

				const int WholeGroupSize = 3;

				var moneyString = money.ToString();

				moneyString = moneyString.Insert(moneyString.Length - 4, " ");

				var startIndex = moneyString.IndexOf(".", StringComparison.Ordinal) - WholeGroupSize;

				if (startIndex > 0)
				{
					for (var i = startIndex; i > 0; i -= WholeGroupSize)
					{
						moneyString = moneyString.Insert(i, " ");
					}
				}

				return moneyString;
			});
	}
}
