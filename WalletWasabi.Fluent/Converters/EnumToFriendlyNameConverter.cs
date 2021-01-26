using System;
using Avalonia.Data.Converters;
using WalletWasabi.Extensions;

namespace WalletWasabi.Fluent.Converters
{
	public static class EnumToFriendlyNameConverter
	{
		public static readonly IValueConverter EnumToFriendlyName =
			new FuncValueConverter<Enum, string>(x => x.FriendlyName());
	}
}