using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters
{
	public static class BoolConverters
	{
		public static readonly IValueConverter Not =
			new FuncValueConverter<bool, bool>(x => !x);
	}
}