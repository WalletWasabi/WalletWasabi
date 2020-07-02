using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class ZkExponentProof
	{
		public ZkExponentProof(GE publicPoint, GE randomPoint, Scalar response)
		{
			Guard.True($"{nameof(publicPoint)}.{nameof(publicPoint.IsValidVariable)}", publicPoint.IsValidVariable);
			Guard.True($"{nameof(randomPoint)}.{nameof(randomPoint.IsValidVariable)}", randomPoint.IsValidVariable);
			Guard.False($"{nameof(response)}.{nameof(response.IsOverflow)}", response.IsOverflow);
			if (Secp256k1Helpers.Equals(publicPoint, randomPoint))
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(randomPoint)} should not be equal.");
			}

			PublicPoint = publicPoint;
			RandomPoint = randomPoint;
			Response = response;
		}

		public GE PublicPoint { get; }
		public GE RandomPoint { get; }
		public Scalar Response { get; }
	}
}
