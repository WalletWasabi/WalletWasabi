using NBitcoin;

namespace WalletWasabi.BitcoinRpc.Models;

public class VerboseOutputInfo
{
	public VerboseOutputInfo(Money value, Script scriptPubKey, string? pubkeyType)
	{
		Value = value;
		ScriptPubKey = scriptPubKey;
		PubkeyType = RpcParser.ConvertPubkeyType(pubkeyType);
	}

	public Money Value { get; }

	public Script ScriptPubKey { get; }

	public RpcPubkeyType PubkeyType { get; }
}
