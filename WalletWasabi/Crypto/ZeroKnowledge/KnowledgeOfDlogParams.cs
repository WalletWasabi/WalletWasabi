using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfDlogParams : KnowledgeOfRepParams
	{
		public KnowledgeOfDlogParams(Scalar secret, Statement statement)
			: base(new[] { secret }, statement)
		{
		}

		public Scalar Secret => Secrets.First();
	}
}
