using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	/// <summary>Identity that exists for the entire application life.</summary>
	/// <remarks>
	/// Use this identity for HTTP requests where your privacy model does not mandate
	/// that HTTP requests can be identified as belonging to the same user.
	/// </remarks>
	public class DefaultIdentity : IIdentity
	{
		public static readonly DefaultIdentity Instance = new();

		private static string RandomName = RandomString.CapitalAlphaNumeric(21);

		private DefaultIdentity()
		{
		}

		public string Name => RandomName;

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(DefaultIdentity)}:{Name}]";
		}
	}
}
