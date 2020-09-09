using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRep
	{
		public KnowledgeOfRep(GroupElement nonce, IEnumerable<Scalar> responses)
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

		public KnowledgeOfRep(GroupElement nonce, params Scalar[] responses)
			: this(nonce, responses as IEnumerable<Scalar>)
		{
		}

		public GroupElement Nonce { get; }
		public IEnumerable<Scalar> Responses { get; }
	}
}
