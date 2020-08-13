using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRepresentation
	{
		public KnowledgeOfRepresentation(GroupElement nonce, IEnumerable<Scalar> responses)
		{
			Nonce = nonce;
			Responses = responses;
		}

		public GroupElement Nonce { get; }
		public IEnumerable<Scalar> Responses { get; }
	}
}
