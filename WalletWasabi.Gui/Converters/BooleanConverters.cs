using Avalonia.Data.Converters;

namespace WalletWasabi.Gui.Converters
{
	public static class BooleanConverters
	{
		public static readonly IValueConverter Not =
			new FuncValueConverter<bool, bool>(x => !x);

		public static IValueConverter Stringify(string trueString, string falseString)
			=> new FuncValueConverter<bool, string>(x => x ? trueString : falseString);

		public static readonly IValueConverter OnOff = Stringify("On", "Off");
		public static readonly IValueConverter ClearMax = Stringify("Clear", "Max");
		public static readonly IValueConverter HideShow = Stringify("Hide", "Show");
	}
}