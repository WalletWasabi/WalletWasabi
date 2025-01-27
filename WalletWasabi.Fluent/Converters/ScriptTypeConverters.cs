using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Converters;

public static class ScriptTypeConverters
{
	public static readonly IValueConverter IsSegwit = new FuncValueConverter<ScriptType, bool>(x => x == ScriptType.SegWit);
	public static readonly IValueConverter IsTaproot = new FuncValueConverter<ScriptType, bool>(x => x == ScriptType.Taproot);
	public static readonly IValueConverter ToName = new FuncValueConverter<ScriptType, string>(x => x is null ? "" : x.Name);
}
