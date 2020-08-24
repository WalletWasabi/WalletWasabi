using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfDlog : KnowledgeOfRep
	{
		public KnowledgeOfDlog(GroupElement nonce, Scalar response)
			: base(nonce, new[] { response })

		{
		}

		public Scalar Response => Responses.First();
	}
}
