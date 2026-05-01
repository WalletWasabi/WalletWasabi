using NBitcoin;
using NBitcoin.WalletPolicies;

namespace WalletWasabi.Blockchain.Keys;

public class WpkhWalletPolicyHelper
{
	/// <seealso href="https://bips.dev/388/"/>
	public static WalletPolicy Get(Network network, HDFingerprint masterFingerprint, ExtKey notDerivedAccountKey, KeyPath accountKeyPath)
	{
		ExtKey accountKey = notDerivedAccountKey.Derive(accountKeyPath);

		// Optional part looks like this, for example: [2fc4a4f3/84'/0'/0']
		string optionalPart = $"[{masterFingerprint}/{accountKeyPath}]";

		// Optional part is followed by the actual private key: "tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk"
		string privateDescriptor = $"{optionalPart}{accountKey.ToString(network)}";

		WalletPolicy walletPolicy = WalletPolicy.Parse($"wpkh({privateDescriptor}/<0;1>/*)", network);

		return walletPolicy;
	}
}
