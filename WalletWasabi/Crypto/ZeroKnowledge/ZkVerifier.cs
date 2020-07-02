using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkVerifier
	{
		public static bool Verify(ZkProof proof)
		{
			Guard.NotNull(nameof(proof), proof);

			var publicPoint = proof.PublicPoint;
			var randomPoint = proof.RandomPoint;
			var concatenation = ByteHelpers.Combine(
				publicPoint.x.ToBytes(),
				publicPoint.y.ToBytes(),
				randomPoint.x.ToBytes(),
				randomPoint.y.ToBytes());
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hash = sha256.ComputeHash(concatenation);
			var challenge = new Scalar(hash);

			var foo = (publicPoint * challenge + randomPoint).ToGroupElement();
			var bar = (EC.G * proof.Response).ToGroupElement();
			return (foo.IsInfinity && bar.IsInfinity) || (foo.x == bar.x && foo.y == bar.y);
		}
	}
}
