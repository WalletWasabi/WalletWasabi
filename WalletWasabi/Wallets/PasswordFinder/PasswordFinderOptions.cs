namespace WalletWasabi.Wallets.PasswordFinder;

public class PasswordFinderOptions
{
	public PasswordFinderOptions(Wallet wallet, string likelyPassword)
	{
		Wallet = wallet;
		LikelyPassword = likelyPassword;
	}

	public Wallet Wallet { get; }

	public Charset Charset { get; set; }

	public bool UseNumbers { get; set; }

	public bool UseSymbols { get; set; }

	public string LikelyPassword { get; }
}
