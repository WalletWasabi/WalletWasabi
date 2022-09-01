using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyManager
{
	public HdPubKeyManager(ExtPubKey extPubKey, RootedKeyPath rootedKeyPath, HdPubKeyCache hdPubKeyCache, int minGapLimit)
	{
		ExtPubKey = extPubKey;
		RootedKeyPath = rootedKeyPath;
		HdPubKeyCache = hdPubKeyCache;
		MinGapLimit = minGapLimit;
	}
	
	public ExtPubKey ExtPubKey { get; }
	public RootedKeyPath RootedKeyPath { get; }
	private HdPubKeyCache HdPubKeyCache { get; }
	public int MinGapLimit { get; }

}
