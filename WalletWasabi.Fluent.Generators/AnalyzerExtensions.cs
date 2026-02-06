using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.Generators.Analyzers;

namespace WalletWasabi.Fluent.Generators;

public static class AnalyzerExtensions
{
	public static List<IdentifierNameSyntax> GetUiContextReferences(this SyntaxNode node, SemanticModel semanticModel)
	{
		var directReferences =
			node.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(x => x.Identifier.ValueText == "UiContext") // faster verification
				.Where(x => semanticModel.GetTypeInfo(x).Type?.ToDisplayString() == UiContextAnalyzer.UiContextType) // slower, but safer. Only runs if previous verification passed.
				.ToList();

		var indirectReferences =
			node.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(x => x.Identifier.ValueText is "Navigate" or "NavigateDialogAsync")
				.Where(x => semanticModel.GetSymbolInfo(x).Symbol?.Kind == SymbolKind.Method)
				.ToList();

		return
			directReferences.Concat(indirectReferences)
							.ToList();
	}

	public static bool IsPrivate(this ConstructorDeclarationSyntax node)
	{
		return node.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));
	}

	public static bool IsPublic(this ConstructorDeclarationSyntax node)
	{
		return node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
	}

	public static bool IsSourceGenerated(this SyntaxNode node)
	{
		var filePath = node.SyntaxTree.FilePath;
		return filePath is null || filePath.EndsWith(UiContextAnalyzer.UiContextFileSuffix);
	}

	public static bool IsAbstractClass(this ClassDeclarationSyntax cls, SemanticModel model)
	{
		var typeInfo = model.GetDeclaredSymbol(cls)
			?? throw new InvalidOperationException($"Unable to get Declared Symbol: {cls.Identifier}");

		return typeInfo.IsAbstract;
	}

	public static bool IsUiContextType(this TypeSyntax? typeSyntax, SemanticModel model)
	{
		if (typeSyntax is null)
		{
			return false;
		}

		return model.GetTypeInfo(typeSyntax).Type?.ToDisplayString() == UiContextAnalyzer.UiContextType;
	}
}
