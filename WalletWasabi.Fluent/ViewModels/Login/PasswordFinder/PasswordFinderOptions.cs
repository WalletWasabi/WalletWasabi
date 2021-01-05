using System.ComponentModel;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public enum Charset
	{
		[Description("English")]
		en,
		[Description("Spanish")]
		es,
		[Description("Italian")]
		it,
		[Description("French")]
		fr,
		[Description("Portuguese")]
		pt
	}

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