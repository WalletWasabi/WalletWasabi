using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfAnd
	{
		public KnowledgeOfAnd(IEnumerable<KnowledgeOfRep> knowledge)
		{
			if (knowledge.Count() < 2)
			{
				throw new ArgumentException($"Relationship can be proven between at least two distinct knowledge.", nameof(knowledge));
			}
			KnowledgeOfRepresentations = knowledge;
		}

		public IEnumerable<KnowledgeOfRep> KnowledgeOfRepresentations { get; }
	}
}
