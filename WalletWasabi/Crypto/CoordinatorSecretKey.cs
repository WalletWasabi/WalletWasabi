using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto
{
	public class CoordinatorSecretKey
	{
		public CoordinatorSecretKey(WasabiRandom rng)
			: this(rng.GetScalar(), rng.GetScalar(), rng.GetScalar(), rng.GetScalar(), rng.GetScalar())
		{
		}

		private CoordinatorSecretKey(Scalar w, Scalar wp, Scalar x0, Scalar x1, Scalar ya)
		{
			this.W = w;
			this.Wp = wp;
			this.X0 = x0;
			this.X1 = x1;
			this.Ya = ya;
		}

		public Scalar W { get; }
		public Scalar Wp { get; }
		public Scalar X0 { get; }
		public Scalar X1 { get; }
		public Scalar Ya { get; }

		public CoordinatorParameters ComputeCoordinatorParameters() =>
			new CoordinatorParameters(
				Cw: (W * Generators.Gw + Wp * Generators.Gwp),
				I: Generators.GV - (X0 * Generators.Gx0 + X1 * Generators.Gx1 + Ya * Generators.Ga));
	}
}
