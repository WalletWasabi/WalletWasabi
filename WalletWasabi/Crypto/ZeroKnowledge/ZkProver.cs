using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkProver
	{
		public static ZkProof CreateProof(Scalar exponent)
		{
			var publicPoint = (EC.G * exponent).ToGroupElement();
			var randomScalar = SecureRandom.GetScalar();
			var randomPoint = (EC.G * randomScalar).ToGroupElement();

			var concatenation = ByteHelpers.Combine(
				publicPoint.x.ToBytes(),
				publicPoint.y.ToBytes(),
				randomPoint.x.ToBytes(),
				randomPoint.y.ToBytes());
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hash = sha256.ComputeHash(concatenation);
			var challenge = new Scalar(hash);
			var response = randomScalar + exponent * challenge;

			return new ZkProof(publicPoint, randomPoint, response);
		}
	}
}
