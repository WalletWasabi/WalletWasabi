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
		public static ZkExponentProof CreateProof(Scalar exponent)
		{
			var publicPoint = (EC.G * exponent).ToGroupElement();
			var randomScalar = SecureRandom.GetScalarNonZero();
			var randomPoint = (EC.G * randomScalar).ToGroupElement();
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var response = randomScalar + exponent * challenge;

			return new ZkExponentProof(publicPoint, randomPoint, response);
		}
	}
}
