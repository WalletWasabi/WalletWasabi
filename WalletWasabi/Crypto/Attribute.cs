using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto
{
	public class Attribute
	{
		private Attribute(Scalar amount, Scalar randomness)
		{
			Randomness = randomness;
			Ma = amount * Generators.Gg + randomness * Generators.Gh;
		}

		public Scalar Randomness { get; }
		public GroupElement Ma { get; }

		public static Attribute FromMoney(Money money, WasabiRandom rng)
			=> new Attribute(new Scalar((ulong)money.Satoshi), rng.GetScalar(allowZero: false));
	}
}
