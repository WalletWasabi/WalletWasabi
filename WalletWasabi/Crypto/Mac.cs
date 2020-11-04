using System;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class MAC : IEquatable<MAC>
	{
		[JsonConstructor]
		private MAC(Scalar t, GroupElement v)
		{
			T = Guard.NotZero(nameof(t), t);
			V = Guard.NotNullOrInfinity(nameof(v), v);
		}

		public Scalar T { get; }
		public GroupElement V { get; }
		public GroupElement U => GenerateU(T);

		public static bool operator ==(MAC? a, MAC? b) => (a?.T, a?.V) == (b?.T, b?.V);

		public static bool operator !=(MAC? a, MAC? b) => !(a == b);

		public override bool Equals(object? obj) => Equals(obj as MAC);

		public bool Equals(MAC? other) => this == other;

		public override int GetHashCode() =>
			HashCode.Combine(T, V).GetHashCode();

		public static MAC ComputeMAC(CoordinatorSecretKey sk, GroupElement ma, Scalar t)
		{
			Guard.NotNull(nameof(sk), sk);
			Guard.NotZero(nameof(t), t);
			Guard.NotNullOrInfinity(nameof(ma), ma);

			return ComputeAlgebraicMAC((sk.X0, sk.X1), (sk.W * Generators.Gw) + (sk.Ya * ma), t);
		}

		public bool VerifyMAC(CoordinatorSecretKey sk, GroupElement ma) =>
			ComputeMAC(sk, ma, T) == this;

		private static MAC ComputeAlgebraicMAC((Scalar x0, Scalar x1) sk, GroupElement m, Scalar t) =>
			 new MAC(t, (sk.x0 + sk.x1 * t) * GenerateU(t) + m);

		public static GroupElement GenerateU(Scalar t) =>
			Generators.FromBuffer(t.ToBytes());
	}
}
