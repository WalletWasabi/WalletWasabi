using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	public class DefaultIdentity : IIdentity
	{
		public static readonly DefaultIdentity Instance = new();

		private static string RandomName = RandomString.CapitalAlphaNumeric(21);

		public string Name => RandomName;

		public bool CanTorCircuitBeReused => true;

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(DefaultIdentity)}:{Name}]";
		}
	}
}
