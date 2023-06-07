using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Generators;

public static class AnalyzerExtensions
{
	public static List<IdentifierNameSyntax> GetUiContextReferences(this SyntaxNode node, SemanticModel semanticModel)
	{
		var directReferences =
			node.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(x => x.Identifier.ValueText == "UiContext")                                                   // faster verification
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

		return filePath is null ||
			   filePath.EndsWith(UiContextAnalyzer.UiContextFileSuffix);
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
		var typeInfo = model.GetDeclaredSymbol(cls)
		               ?? throw new InvalidOperationException($"Unable to get Declared Symbol: {cls.Identifier}");

		return typeInfo.IsAbstract;
	}

	public static bool IsRoutableViewModel(this SyntaxNode node, SemanticModel model)
	{
		return node.IsSubTypeOf(model, "WalletWasabi.Fluent.ViewModels.Navigation.RoutableViewModel");
	}

	public static (string? TypeName, IEnumerable<string> Namespaces) GetDialogResultType(this SyntaxNode node, SemanticModel model)
	{
		if (node is not ClassDeclarationSyntax cls)
		{
			return (null, Array.Empty<string>());
		}

		var currentType = model.GetDeclaredSymbol(cls);
		while (currentType != null)
		{
			if (currentType.ConstructedFrom?.ToDisplayString() == UiContextAnalyzer.DialogViewModelBaseType)
			{
				var typeArgument = currentType.TypeArguments.FirstOrDefault();
				if (typeArgument is { })
				{
					return (typeArgument.ToDisplayString(), typeArgument.GetNamespaces());
				}
			}
			currentType = currentType.BaseType;
		}

		return (null, Array.Empty<string>());
	}

	public static bool HasUiContextParameter(this ConstructorDeclarationSyntax ctor, SemanticModel model)
	{
		return ctor.ParameterList.Parameters.Any(p => p.Type.IsUiContextType(model));
	}

	public static bool IsUiContextType(this TypeSyntax? typeSyntax, SemanticModel model)
	{
		if (typeSyntax is null)
		{
			return false;
		}

		return model.GetTypeInfo(typeSyntax).Type?.ToDisplayString() == UiContextAnalyzer.UiContextType;
	}

	public static List<string> GetNamespaces(this ITypeSymbol? typeSymbol)
	{
		return GetNamespaceSymbols(typeSymbol)
			.Where(x => !x.IsGlobalNamespace)
			.Select(x => x.ToDisplayString())
			.ToList();
	}

	private static IEnumerable<INamespaceSymbol> GetNamespaceSymbols(this ITypeSymbol? typeSymbol)
	{
		if (typeSymbol is null)
		{
			yield break;
		}

		yield return typeSymbol.ContainingNamespace;

		if (typeSymbol is INamedTypeSymbol namedType)
		{
			foreach (var typeArg in namedType.TypeArguments)
			{
				yield return typeArg.ContainingNamespace;
			}
		}
	}
}
