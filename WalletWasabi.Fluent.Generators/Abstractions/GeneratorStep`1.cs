using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace WalletWasabi.Fluent.Generators;

internal abstract class GeneratorStep<T> : GeneratorStep where T : SyntaxNode
{
	private List<T> _nodes = new();

	public override sealed void OnVisitSyntaxNode(SyntaxNode syntaxNode)
	{
		if (syntaxNode is not T node)
		{
			return;
		}

		if (Filter(node))
		{
			_nodes.Add(node);
		}
	}

	public override void Execute()
	{
		Execute(_nodes.ToArray());
	}

	public abstract void Execute(T[] syntaxNodes);

	public virtual bool Filter(T node) => true;
}
