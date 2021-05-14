using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	/// <summary>Random idendity for an HTTP requests which should not be linked with any other HTTP request.</summary>
	public class OneOffIdentity : IIdentity
	{		
		public OneOffIdentity()
		{
			Name = RandomString.CapitalAlphaNumeric(21);
		}
		public string Name { get; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(OneOffIdentity)}: {Name}]";
		}
	}
}
