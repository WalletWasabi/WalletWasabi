namespace WalletWasabi.Wallets.PasswordFinder
{
	public class PasswordFinderOptions
	{
		public PasswordFinderOptions(Wallet wallet, string password)
		{
			Wallet = wallet;
			Password = password;
		}

		public Wallet Wallet { get; }

		public Charset Charset { get; set; }

		public bool UseNumbers { get; set; }

		public bool UseSymbols { get; set; }

		public string Password { get; }
	}
}
