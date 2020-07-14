using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkProver
	{
		public static ZkExponentProof CreateProof(Scalar exponent)
		{
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsOverflow)}", exponent.IsOverflow);
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsZero)}", exponent.IsZero);

			var publicPoint = (EC.G * exponent).ToGroupElement();
			var secureRandom = new SecureRandom();
			var randomScalar = (secureRandom as IWasabiRandom).GetScalar();
			var randomPoint = (EC.G * randomScalar).ToGroupElement();
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var response = randomScalar + exponent * challenge;

			return new ZkExponentProof(publicPoint, randomPoint, response);
		}
	}
}
