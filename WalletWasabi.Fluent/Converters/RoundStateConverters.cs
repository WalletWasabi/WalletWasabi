using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Converters;

public static class RoundStateConverters
{
	public static readonly IValueConverter ToShortId =
		new FuncValueConverter<uint256?, string?>(x =>
		{
			var id = x?.ToString();
			return id?.Substring(Math.Max(0, id.Length - 6));
		});
}
