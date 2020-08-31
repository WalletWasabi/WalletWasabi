using System;
using System.Text;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class MAC : IEquatable<MAC>
	{
		private MAC(Scalar t, GroupElement U, GroupElement V)
		{
			this.t = CryptoGuard.NotZero(nameof(t), t);
			this.U = CryptoGuard.NotNullOrInfinity(nameof(U), U);
			this.V = CryptoGuard.NotNullOrInfinity(nameof(V), V);
		}

		public Scalar t { get; }
		public GroupElement U { get; }
		public GroupElement V { get; }

		public static bool operator == (MAC a, MAC b) => a.Equals(b);
		public static bool operator != (MAC a, MAC b) => !a.Equals(b);

		public bool Equals(MAC? other) =>
			(this, other) switch
			{
				(null, null) => true,
				(null, {}) => false,
				({}, null) => false,
				({} a , {} b) when Object.ReferenceEquals(a, b) => true, 
				({} a , {} b) => (a.t, a.U, a.V) == (b.t, b.U, b.V)
			};

		public override bool Equals(object? obj) =>
			Equals(obj as MAC);

		public override int GetHashCode() =>
			HashCode.Combine(t, U, V).GetHashCode();

		public static MAC ComputeMAC(CoordinatorSecretKey sk, GroupElement Ma, Scalar t)
		{
			Guard.NotNull(nameof(sk), sk);
			CryptoGuard.NotNullOrInfinity(nameof(Ma), Ma);

			var numsGeneratorSeed = Encoding.UTF8.GetString(t.ToBytes());
			return ComputeAlgebraicMAC((sk.X0, sk.X1), (sk.W * Generators.Gw) + (sk.Ya * Ma), t, GroupElement.FromText(numsGeneratorSeed));
		}

		public bool VerifyMAC(CoordinatorSecretKey sk, GroupElement Ma)
			=> ComputeMAC(sk, Ma, t) == this;

		private static MAC ComputeAlgebraicMAC((Scalar x0, Scalar x1) sk, GroupElement M, Scalar t, GroupElement U)
			=> new MAC(t, U, (sk.x0 + sk.x1 * t) * U + M);
	}
}