namespace WalletWasabi.Fluent.Models.Wallets;

public record ScriptType(string Name, string ShortName)
{
	public static readonly ScriptType Unknown = new("Unknown", "?");
	public static ScriptType SegWit = new("SegWit", "SW");
	public static ScriptType Taproot = new("Taproot", "TR");

	public static ScriptType FromEnum(NBitcoin.ScriptType type)
	{
		return type switch
		{
			NBitcoin.ScriptType.Witness => Unknown,
			NBitcoin.ScriptType.P2PKH => Unknown,
			NBitcoin.ScriptType.P2SH => Unknown,
			NBitcoin.ScriptType.P2PK => Unknown,
			NBitcoin.ScriptType.MultiSig => Unknown,
			NBitcoin.ScriptType.P2WSH => SegWit,
			NBitcoin.ScriptType.P2WPKH => SegWit,
			NBitcoin.ScriptType.Taproot => Taproot,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}
}
