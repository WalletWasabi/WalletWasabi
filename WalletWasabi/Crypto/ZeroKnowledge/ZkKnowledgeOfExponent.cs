using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class ZkKnowledgeOfExponent
	{
		public ZkKnowledgeOfExponent(GroupElement randomPoint, Scalar response)
		{
			Guard.False($"{nameof(randomPoint)}.{nameof(randomPoint.IsInfinity)}", randomPoint.IsInfinity);
			Guard.False($"{nameof(response)}.{nameof(response.IsZero)}", response.IsZero);

			RandomPoint = randomPoint;
			Response = response;
		}

		public GroupElement RandomPoint { get; }
		public Scalar Response { get; }
	}
}
