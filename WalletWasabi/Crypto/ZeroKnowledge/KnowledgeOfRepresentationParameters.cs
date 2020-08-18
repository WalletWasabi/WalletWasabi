using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRepresentationParameters
	{
		public KnowledgeOfRepresentationParameters(IEnumerable<(Scalar secret, GroupElement generator)> secretGeneratorPairs, GroupElement publicPoint)
		{
			SecretGeneratorPairs = secretGeneratorPairs;
			PublicPoint = publicPoint;
		}

		public IEnumerable<(Scalar secret, GroupElement generator)> SecretGeneratorPairs { get; }
		public GroupElement PublicPoint { get; }
	}
}
