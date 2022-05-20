using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WalletWasabi.Fluent.Generators;

internal abstract class FieldsWithAttributeSyntaxReceiver : ISyntaxContextReceiver
{
	public abstract string AttributeClass { get; }

	public List<IFieldSymbol> FieldSymbols { get; } = new();

	public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
	{
		if (context.Node is FieldDeclarationSyntax fieldDeclarationSyntax
		    && fieldDeclarationSyntax.AttributeLists.Count > 0)
		{
			foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
			{
				if (context.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
				{
					continue;
				}

				var attributes = fieldSymbol.GetAttributes();
				if (attributes.Any(ad => ad?.AttributeClass?.ToDisplayString() == AttributeClass))
				{
					FieldSymbols.Add(fieldSymbol);
				}
			}
		}
	}
}