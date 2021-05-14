using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	/// <summary>
	/// Identity for a set of HTTP requests where we don't mind that
	/// HTTP requests can be identified as belonging to a single user.
	/// </summary>
	/// <remarks>Useful for Alices and Bobs.</remarks>
	public class PersonIdentity : IIdentity
	{		
		public PersonIdentity()
		{
			Name = RandomString.CapitalAlphaNumeric(21);
		}
		public string Name { get; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(PersonIdentity)}: {Name}]";
		}
	}
}
