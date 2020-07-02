using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class ZkProof
	{
		public ZkProof(GE publicPoint, GE randomPoint, Scalar response)
		{
			PublicPoint = publicPoint;
			RandomPoint = randomPoint;
			Response = response;
		}

		public GE PublicPoint { get; }
		public GE RandomPoint { get; }
		public Scalar Response { get; }
	}
}
