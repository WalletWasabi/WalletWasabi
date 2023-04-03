using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Generators;

public static class AnalyzerExtensions
{
	public static List<IdentifierNameSyntax> GetUIContextReferences(this SyntaxNode node, SemanticModel semanticModel)
	{
		return
			node.DescendantNodes()
			 .OfType<IdentifierNameSyntax>()
			 .Where(x => x.Identifier.ValueText == "UIContext")                                                   // faster verification
			 .Where(x => semanticModel.GetTypeInfo(x).Type?.ToDisplayString() == UIContextAnalyzer.UIContextType) // slower, but safer. Only runs if previous verification passed.
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

		return filePath is null ||
			   filePath.EndsWith(UIContextAnalyzer.UIContextFileSuffix);
	}

	public static bool IsSubTypeOf(this SyntaxNode node, SemanticModel model, string baseType)
	{
		if (node is not ClassDeclarationSyntax cls)
		{
			return false;
		}

		var currentType = model.GetDeclaredSymbol(cls);
		while (currentType != null)
		{
			if (currentType.ToDisplayString() == baseType)
			{
				return true;
			}
			currentType = currentType.BaseType;
		}

		return false;
	}

	public static bool IsAbstractClass(this ClassDeclarationSyntax cls, SemanticModel model)
	{
		var typeInfo =
			model.GetDeclaredSymbol(cls)
			?? throw new InvalidOperationException($"Unable to get Declared Symbol: {cls.Identifier}");

		return typeInfo.IsAbstract;
	}

	public static bool IsRoutableViewModel(this SyntaxNode node, SemanticModel model)
	{
		return node.IsSubTypeOf(model, "WalletWasabi.Fluent.ViewModels.Navigation.RoutableViewModel");
	}

	public static bool HasUIContextParameter(this ConstructorDeclarationSyntax ctor, SemanticModel model)
	{
		return ctor.ParameterList.Parameters.Any(p => p.Type.IsUIContextType(model));
	}

	public static bool IsUIContextType(this TypeSyntax? typeSyntax, SemanticModel model)
	{
		if (typeSyntax is null)
		{
			return false;
		}

		return model.GetTypeInfo(typeSyntax).Type?.ToDisplayString() == UIContextAnalyzer.UIContextType;
	}

	public static IEnumerable<string> GetNamespaces(this ITypeSymbol? typeSymbol)
	{
		if (typeSymbol is null)
		{
			yield break;
		}

		yield return typeSymbol.ContainingNamespace.ToDisplayString();

		if (typeSymbol is INamedTypeSymbol namedType)
		{
			foreach (var typeArg in namedType.TypeArguments)
			{
				yield return typeArg.ContainingNamespace.ToDisplayString();
			}
		}
	}
}
