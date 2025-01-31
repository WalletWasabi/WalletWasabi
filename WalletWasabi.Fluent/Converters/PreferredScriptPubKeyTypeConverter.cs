using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Converters;

public class PreferredScriptPubKeyTypeConverter
{
	public static readonly IValueConverter ToName = new FuncValueConverter<PreferredScriptPubKeyType, string>(x =>
		x switch
		{
			PreferredScriptPubKeyType.Unspecified s => "SegWit & Taproot",
			PreferredScriptPubKeyType.Specified s => s.ScriptType switch
			{
				ScriptPubKeyType.TaprootBIP86 => "Taproot Only",
				ScriptPubKeyType.Segwit => "SegWit Only",
				_ => s.Name,
			},
			_ => throw new ArgumentOutOfRangeException()
		});
}
