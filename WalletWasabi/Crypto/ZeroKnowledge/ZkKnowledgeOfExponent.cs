using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class ZkKnowledgeOfExponent
	{
		public ZkKnowledgeOfExponent(GroupElement nonce, Scalar response)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			Guard.False($"{nameof(response)}.{nameof(response.IsZero)}", response.IsZero);

			Nonce = nonce;
			Response = response;
		}

		public GroupElement Nonce { get; }
		public Scalar Response { get; }
	}
}
