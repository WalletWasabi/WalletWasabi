using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto
{
	public class CoordinatorSecretKey
	{
		public CoordinatorSecretKey(WasabiRandom rng)
			: this(rng.GetScalar(), rng.GetScalar(), rng.GetScalar(), rng.GetScalar(), rng.GetScalar())
		{}

		private CoordinatorSecretKey(Scalar w, Scalar wp, Scalar x0, Scalar x1, Scalar ya)
		{
			this.w  = w;
			this.wp = wp;
			this.x0 = x0;
			this.x1 = x1;
			this.ya = ya;
		}

		public Scalar w { get; }
		public Scalar wp { get; }
		public Scalar x0 { get; }
		public Scalar x1 { get; }
		public Scalar ya { get; }
		
		public CoordinatorParameters ComputeCoordinatorParameters() =>
			new CoordinatorParameters(
				Cw: (w * Generators.Gw + wp * Generators.Gwp),
				I:	(x0.Negate() * Generators.Gx0) +
					(x1.Negate() * Generators.Gx1) +
					(ya.Negate() * Generators.Ga ) +
					Generators.GV );
	}
}
