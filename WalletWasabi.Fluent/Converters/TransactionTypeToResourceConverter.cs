using System;
using System.IO;
using System.Linq;
using System.Resources;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Styling;
using WalletWasabi.Fluent.Model;

namespace WalletWasabi.Fluent.Converters
{
	public class TransactionTypeToResourceConverter
	{
		public static readonly IValueConverter ToResource =
			new FuncValueConverter<TransactionType, object?>(type =>
			{
				string key;

				switch (type)
				{
					case TransactionType.Incoming:
						key = "arrow_down_right_regular";
						break;
					case TransactionType.Outgoing:
						key = "arrow_up_right_regular";
						break;
					case TransactionType.SelfSpend:
						key = "normal_transaction";
						break;
					case TransactionType.CoinJoin:
						key = "shield_regular";
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(type), type, null);
				}

				var icons = Application.Current.Styles.Select(x => (StyleInclude) x).FirstOrDefault(x => x.Source is { } && x.Source.AbsolutePath.Contains("Icons"));

				if (icons is { } && icons.TryGetResource(key, out var resource))
				{
					return resource;
				}

				throw new InvalidDataException("Resource not found!");
			});
	}
}
