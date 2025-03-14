using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public record HdPubKeyInfo
{
	public HdPubKeyInfo(HdPubKey hdPubKey, ScriptPubKeyType scriptPubKeyType)
	{
		HdPubKey = hdPubKey;
		ScriptPubKeyType = scriptPubKeyType;
		ScriptPubKey = hdPubKey.PubKey.GetScriptPubKey(scriptPubKeyType);
		CompressedScriptPubKeyBytes = ScriptPubKey.ToCompressedBytes();
	}
	public HdPubKey HdPubKey { get; }
	public ScriptPubKeyType ScriptPubKeyType { get; set; }
	public Script ScriptPubKey { get; }
	public byte[] ScriptPubKeyBytes => ScriptPubKey.ToBytes(@unsafe: true);
	public byte[] CompressedScriptPubKeyBytes { get; }
}
