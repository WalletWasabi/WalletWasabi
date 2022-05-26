using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto;

public record CredentialIssuerSecretKey
{
	public CredentialIssuerSecretKey(WasabiRandom rng)
		: this(rng.GetScalar(), rng.GetScalar(), rng.GetScalar(), rng.GetScalar(), rng.GetScalar())
	{
	}

	private CredentialIssuerSecretKey(Scalar w, Scalar wp, Scalar x0, Scalar x1, Scalar ya)
	{
		W = Guard.NotZero(nameof(w), w);
		Wp = Guard.NotZero(nameof(wp), wp);
		X0 = Guard.NotZero(nameof(x0), x0);
		X1 = Guard.NotZero(nameof(x1), x1);
		Ya = Guard.NotZero(nameof(ya), ya);
	}

	public Scalar W { get; }
	public Scalar Wp { get; }
	public Scalar X0 { get; }
	public Scalar X1 { get; }
	public Scalar Ya { get; }

	public CredentialIssuerParameters ComputeCredentialIssuerParameters() =>
		new(
			cw: W * Generators.Gw + Wp * Generators.Gwp,
			i: Generators.GV - (X0 * Generators.Gx0 + X1 * Generators.Gx1 + Ya * Generators.Ga));

	internal ScalarVector AsScalarVector() =>
		new(W, Wp, X0, X1, Ya);
}
