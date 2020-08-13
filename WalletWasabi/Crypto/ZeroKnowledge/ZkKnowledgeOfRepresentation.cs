using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class ZkKnowledgeOfRepresentation
	{
		public ZkKnowledgeOfRepresentation(GroupElement randomness, IEnumerable<Scalar> responses)
		{
			Randomness = randomness;
			Responses = responses;
		}

		public GroupElement Randomness { get; }
		public IEnumerable<Scalar> Responses { get; }
	}
}
