using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto;

public record MAC
{
	[JsonConstructor]
	private MAC(Scalar t, GroupElement v)
	{
		T = Guard.NotZero(nameof(t), t);
		V = Guard.NotNullOrInfinity(nameof(v), v);
	}

	public Scalar T { get; }
	public GroupElement V { get; }
	internal GroupElement U => GenerateU(T);

	public static MAC ComputeMAC(CredentialIssuerSecretKey sk, GroupElement ma, Scalar t)
	{
		Guard.NotNull(nameof(sk), sk);
		Guard.NotZero(nameof(t), t);
		Guard.NotNullOrInfinity(nameof(ma), ma);

		return ComputeAlgebraicMAC((sk.X0, sk.X1), (sk.W * Generators.Gw) + (sk.Ya * ma), t);
	}

	public bool VerifyMAC(CredentialIssuerSecretKey sk, GroupElement ma) =>
		ComputeMAC(sk, ma, T) == this;

	private static MAC ComputeAlgebraicMAC((Scalar x0, Scalar x1) sk, GroupElement m, Scalar t) =>
		 new(t, (sk.x0 + sk.x1 * t) * GenerateU(t) + m);

	public static GroupElement GenerateU(Scalar t) =>
		Generators.FromBuffer(t.ToBytes());
}
