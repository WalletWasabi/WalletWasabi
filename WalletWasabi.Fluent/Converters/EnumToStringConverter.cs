using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Extensions;

namespace WalletWasabi.Fluent.Converters
{
	public static class EnumToStringConverter
	{
		public static readonly IValueConverter ToFriendlyName =
			new FuncValueConverter<Enum, string>(x => x.FriendlyName());

		public static readonly IValueConverter ToUpperCase =
			new FuncValueConverter<Enum, string>(x => x.ToString().ToUpper(CultureInfo.InvariantCulture));
	}
}