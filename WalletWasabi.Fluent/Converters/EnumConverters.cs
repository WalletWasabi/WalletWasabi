using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.Converters;

public static class EnumConverters
{
	public static readonly IValueConverter ToFriendlyName =
		new FuncValueConverter<Enum, string>(x => x?.FriendlyName() ?? "");

	public static readonly IValueConverter ToUpperCase =
		new FuncValueConverter<Enum, string>(x => x?.ToString().ToUpper(CultureInfo.InvariantCulture) ?? "");

	public static readonly IValueConverter ToCharset =
		new FuncValueConverter<Enum, string>(x =>
		{
			if (x is Charset c && PasswordFinderHelper.Charsets.ContainsKey(c))
			{
				return PasswordFinderHelper.Charsets[c];
			}

			return "";
		});
}
