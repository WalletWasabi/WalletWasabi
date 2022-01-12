using WalletWasabi.Models;

namespace WalletWasabi.Wallets.PasswordFinder;

public enum Charset
{
	[FriendlyName("English")]
	en,

	[FriendlyName("Spanish")]
	es,

	[FriendlyName("Italian")]
	it,

	[FriendlyName("French")]
	fr,

	[FriendlyName("Portuguese")]
	pt
}
