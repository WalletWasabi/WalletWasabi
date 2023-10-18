using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace WalletWasabi.Fluent.Generators.Abstractions;

internal abstract class GeneratorStep<T> : GeneratorStep, ISyntaxReceiver where T : SyntaxNode
{
	private List<T> _nodes = new();

	public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
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
