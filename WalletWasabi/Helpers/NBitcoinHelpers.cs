using NBitcoin;

namespace WalletWasabi.Helpers;

public static class NBitcoinHelpers
{
	public static ExtPubKey BetterParseExtPubKey(string extPubKeyString)
	{
		extPubKeyString = Guard.NotNullOrEmptyOrWhitespace(nameof(extPubKeyString), extPubKeyString, trim: true);

		ExtPubKey epk;
		try
		{
			epk = ExtPubKey.Parse(extPubKeyString, Network.Main); // Starts with "ExtPubKey": "xpub...
		}
		catch
		{
			try
			{
				epk = ExtPubKey.Parse(extPubKeyString, Network.TestNet); // Starts with "ExtPubKey": "xpub...
			}
			catch
			{
				try
				{
					epk = ExtPubKey.Parse(extPubKeyString, Network.RegTest); // Starts with "ExtPubKey": "xpub...
				}
				catch
				{
					// Try hex, Old wallet format was like this.
					epk = new ExtPubKey(Convert.FromHexString(extPubKeyString)); // Starts with "ExtPubKey": "hexbytes...
				}
			}
		}

		return epk;
	}
}
