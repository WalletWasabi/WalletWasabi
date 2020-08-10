using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class ZkExponentProof
	{
		public ZkExponentProof(GroupElement publicPoint, GroupElement randomPoint, Scalar response)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			Guard.False($"{nameof(randomPoint)}.{nameof(randomPoint.IsInfinity)}", randomPoint.IsInfinity);
			Guard.False($"{nameof(response)}.{nameof(response.IsOverflow)}", response.IsOverflow);
			if (publicPoint == randomPoint)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(randomPoint)} should not be equal.");
			}

			PublicPoint = publicPoint;
			RandomPoint = randomPoint;
			Response = response;
		}

		public GroupElement PublicPoint { get; }
		public GroupElement RandomPoint { get; }
		public Scalar Response { get; }
	}
}
