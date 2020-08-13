using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRepresentation
	{
		public KnowledgeOfRepresentation(GroupElement nonce, IEnumerable<Scalar> responses)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			Guard.NotNullOrEmpty(nameof(responses), responses);
			foreach (var response in responses)
			{
				Guard.False($"{nameof(response)}.{nameof(response.IsZero)}", response.IsZero);
			}

			Nonce = nonce;
			Responses = responses;
		}

		public GroupElement Nonce { get; }
		public IEnumerable<Scalar> Responses { get; }
	}
}
