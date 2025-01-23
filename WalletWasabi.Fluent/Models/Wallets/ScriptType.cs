using NBitcoin;

namespace WalletWasabi.Fluent.Models.Wallets;

public record ScriptType(string Name, string ShortName)
{
	public static readonly ScriptType Unknown = new("Unknown", "?");
	public static ScriptType SegWit = new("SegWit", "SW");
	public static ScriptType Taproot = new("Taproot", "TR");

	public static ScriptPubKeyType ToScriptPubKeyType(ScriptType scriptType)
	{
		if (scriptType == SegWit)
		{
			return ScriptPubKeyType.Segwit;
		}

		if (scriptType == Taproot)
		{
			return ScriptPubKeyType.TaprootBIP86;
		}

		throw new InvalidOperationException($"Cannot cast the ScriptType {scriptType.Name} to a ScriptPubKeyType");
	}

	public static ScriptType FromString(string str)
	{
		return str switch
		{
			"SegWit" => SegWit,
			"Taproot" => Taproot,
			_ => throw new InvalidOperationException($"Cannot cast the string {str} to a ScriptType")
		};
	}

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

	public static ScriptType FromEnum(ScriptPubKeyType type)
	{
		return type switch
		{
			ScriptPubKeyType.Legacy => Unknown,
			ScriptPubKeyType.Segwit => SegWit,
			ScriptPubKeyType.SegwitP2SH => Unknown,
			ScriptPubKeyType.TaprootBIP86 => Taproot,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}
}
