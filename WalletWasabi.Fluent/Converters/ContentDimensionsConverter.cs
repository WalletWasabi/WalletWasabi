using System;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Converters
{
	public static class ContentDimensionsConverter
	{
		public static readonly IMultiValueConverter Instance =
			new FuncMultiValueConverter<object, double>(parts =>
			{
				var inputs = parts.ToArray();
				return 700;
			});

	}
}
