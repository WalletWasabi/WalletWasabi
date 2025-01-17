using NBitcoin;

namespace WalletWasabi.BitcoinCore.Rpc.Models;

public record VerboseOutputInfo
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
