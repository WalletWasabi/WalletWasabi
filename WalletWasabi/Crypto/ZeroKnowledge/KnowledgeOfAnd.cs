using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfAnd
	{
		public KnowledgeOfAnd(IEnumerable<KnowledgeOfRepresentation> knowledgeOfRepresentations)
		{
			KnowledgeOfRepresentations = knowledgeOfRepresentations;
		}

		public IEnumerable<KnowledgeOfRepresentation> KnowledgeOfRepresentations { get; }
	}
}
