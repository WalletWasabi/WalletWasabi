using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	public class RandomIdentity : IIdentity
	{		
		public RandomIdentity(bool canTorCircuitBeReused)
		{
			Name = RandomString.CapitalAlphaNumeric(21);
			CanTorCircuitBeReused = canTorCircuitBeReused;
		}
		public string Name { get; }

		public bool CanTorCircuitBeReused { get; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(RandomIdentity)}: {Name}]";
		}
	}
}
