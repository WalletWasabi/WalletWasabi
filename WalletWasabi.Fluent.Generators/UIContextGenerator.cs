using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace WalletWasabi.Fluent.Generators;

[Generator]
public class UiContextGenerator : ISourceGenerator
{
	private static readonly string[] Exclusions =
		{
			"RoutableViewModel"
		};

	public void Initialize(GeneratorInitializationContext context)
	{
		context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
	}

	public void Execute(GeneratorExecutionContext context)
	{
		if (context.SyntaxContextReceiver is not SyntaxReceiver receiver)
		{
			return;
		}

		var ctors =
			ProcessViewModels(context, receiver.ClassDeclarations)
				.OrderBy(x => x.Identifier.ValueText)
				.ToList();

		GenerateFluentNavigation(context, ctors);
	}

	private static List<ConstructorDeclarationSyntax> ProcessViewModels(GeneratorExecutionContext context, List<ClassDeclarationSyntax> classDeclarations)
	{
		var result = new List<ConstructorDeclarationSyntax>();

		var toGenerate =
			from cls in classDeclarations
			group cls by cls.Identifier.ValueText into g
			select g.First();

		foreach (var cls in toGenerate)
		{
			var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);

			if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol classSymbol)
			{
				continue;
			}

			var ctors = GenerateConstructors(context, cls, model, classSymbol).ToList();

			result.AddRange(ctors);
		}

		return result;
	}

	private static IEnumerable<ConstructorDeclarationSyntax> GenerateConstructors(GeneratorExecutionContext context, ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel, INamedTypeSymbol classSymbol)
	{
		var fileName = classDeclaration.Identifier.ValueText + UiContextAnalyzer.UiContextFileSuffix;

		var className = classSymbol.Name;
		var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

		var ctors =
			classDeclaration.ChildNodes()
							.OfType<ConstructorDeclarationSyntax>()
							.ToList();

		foreach (var ctor in ctors)
		{
			if (!classDeclaration.GetUiContextReferences(semanticModel).Any())
			{
				if (!classDeclaration.IsAbstractClass(semanticModel) && ctor.IsPublic())
				{
					yield return ctor;
				}
			}
			// if constructor already has a UIContext parameter, leave it be. Don't generate a new ctor and use the current one for FluentNavigation.
			else if (ctor.ParameterList.Parameters.Any(p => p.Type.IsUiContextType(semanticModel)))
			{
				// it must be public though
				if (ctor.IsPublic())
				{
					yield return ctor;
				}
			}
			else
			{
				var ctorArgs =
					  ctor.ParameterList.Parameters
						  .Select(x => x.Identifier.ValueText)
						  .ToList();

				var hasCtorArgs = ctorArgs.Any();
				var ctorArgsString = string.Join(",", ctorArgs);
				var ctorString =
					hasCtorArgs
					? $": this({ctorArgsString})"
					: "";

				var parameterUsings =
					ctor.ParameterList.Parameters
									  .Where(p => p.Type is not null)
									  .Select(p => semanticModel.GetTypeInfo(p.Type!))
									  .Where(t => t.Type is not null)
									  .Select(t => $"using {t.Type!.ContainingNamespace.ToDisplayString()};")
									  .ToList();

				var uiContextParameter =
					SyntaxFactory.Parameter(SyntaxFactory.Identifier("uiContext")
														 .WithLeadingTrivia(SyntaxFactory.Space))
								 .WithType(SyntaxFactory.ParseTypeName("UiContext"));

				var parametersString =
					ctor.ParameterList.Parameters.Insert(0, uiContextParameter).ToFullString();

				var usings = string.Join(Environment.NewLine, parameterUsings.Distinct().OrderBy(x => x));

				var code =
					$$"""
					{{usings}}
					using WalletWasabi.Fluent.Models.UI;

					namespace {{namespaceName}};

					partial class {{className}}
					{
						public {{className}}({{parametersString}}){{ctorString}}
						{
							UiContext = uiContext;
						}
					}
					""";

				var sourceText = SourceText.From(code, Encoding.UTF8);
				context.AddSource(fileName, sourceText);

				var tree = CSharpSyntaxTree.ParseText(sourceText, context.Compilation.SyntaxTrees.First().Options as CSharpParseOptions);

				var newConstructor =
					tree.GetRoot()
						.DescendantNodes()
						.OfType<ConstructorDeclarationSyntax>()
						.First();

				yield return newConstructor;
			}
		}
	}

	private static void GenerateFluentNavigation(GeneratorExecutionContext context, IEnumerable<ConstructorDeclarationSyntax> ctors)
	{
		var compilation = context.Compilation;
		var namespaces = new List<string>();
		var methods = new List<string>();

		var newSyntaxTrees =
			ctors.Select(x => x.SyntaxTree)
				 .Where(x => !compilation.ContainsSyntaxTree(x))
				 .ToList();

		compilation = compilation.AddSyntaxTrees(newSyntaxTrees);

		foreach (var ctor in ctors)
		{
			var semanticModel = compilation.GetSemanticModel(ctor.SyntaxTree);

			if (ctor.Parent is not ClassDeclarationSyntax cls)
			{
				continue;
			}

			if (!cls.IsRoutableViewModel(semanticModel))
			{
				continue;
			}

			var viewModelTypeInfo =
				semanticModel.GetDeclaredSymbol(cls);

			if (viewModelTypeInfo == null)
			{
				continue;
			}

			var className = cls.Identifier.ValueText;

			var ctorNamespaces =
					ctor.ParameterList.Parameters
									  .Where(p => p.Type is not null)
									  .Select(p => semanticModel.GetTypeInfo(p.Type!))
									  .Where(t => t.Type is not null)
									  .SelectMany(t => t.Type.GetNamespaces())
									  .ToList();

			var uiContextParam =
				ctor.ParameterList
					.Parameters
					.FirstOrDefault(x => x.Type.IsUiContextType(semanticModel));

			var methodParams = ctor.ParameterList;

			if (uiContextParam != null)
			{
				methodParams = SyntaxFactory.ParameterList(methodParams.Parameters.Remove(uiContextParam));
			}

			var navigationMetadata =
				viewModelTypeInfo.GetAttributes()
								 .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == NavigationMetaDataGenerator.NavigationMetaDataAttributeDisplayString);

			var defaultNavigationTarget = "DialogScreen";

			if (navigationMetadata != null)
			{
				var navigationArgument =
					navigationMetadata.NamedArguments
									  .FirstOrDefault(x => x.Key == "NavigationTarget");

				if (navigationArgument.Value.Type is INamedTypeSymbol navigationTargetEnum)
				{
					var enumValue =
						navigationTargetEnum.GetMembers()
											.OfType<IFieldSymbol>()
											.FirstOrDefault(x => x.ConstantValue?.Equals(navigationArgument.Value.Value) ?? false);

					if (enumValue != null)
					{
						defaultNavigationTarget = enumValue.Name;
					}
				}
			}

			var additionalMethodParams =
				$"NavigationTarget navigationTarget = NavigationTarget.{defaultNavigationTarget}, NavigationMode navigationMode = NavigationMode.Normal";

			methodParams = methodParams.AddParameters(SyntaxFactory.ParseParameterList(additionalMethodParams).Parameters.ToArray());

			var ctorArgs =
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SeparatedList(
					ctor.ParameterList
						.Parameters
						.Select(x => x.Type.IsUiContextType(semanticModel) ? "UiContext" : x.Identifier.ValueText) // replace uiContext argument for UiContext property reference
						.Select(x => SyntaxFactory.ParseExpression(x))
						.Select(SyntaxFactory.Argument),
					ctor.ParameterList
						.Parameters
						.Skip(1)
						.Select(x => SyntaxFactory.Token(SyntaxKind.CommaToken))));

			namespaces.Add(viewModelTypeInfo.ContainingNamespace.ToDisplayString());
			namespaces.AddRange(ctorNamespaces);

			var methodName = className.Replace("ViewModel", "");

			var methodString =
				$$"""
					public void {{methodName}}{{methodParams}}
					{
						UiContext.Navigate(navigationTarget).To(new {{className}}{{ctorArgs.ToFullString()}}, navigationMode);
					}

				""";
			methods.Add(methodString);
		}

		var usings =
			namespaces.Distinct()
					  .OrderBy(x => x)
					  .Select(n => $"using {n};")
					  .ToList();

		var usingsString =
			string.Join(Environment.NewLine, usings);

		var methodsString =
			string.Join(Environment.NewLine, methods);

		var sourceText = $$"""
			// <auto-generated />
			#nullable enable

			{{usingsString}}

			namespace WalletWasabi.Fluent.ViewModels.Navigation;

			public partial class FluentNavigate
			{
			{{methodsString}}
			}

			""";
		context.AddSource("FluentNavigate.g.cs", SourceText.From(sourceText, Encoding.UTF8));
	}

	private class SyntaxReceiver : ISyntaxContextReceiver
	{
		public List<ClassDeclarationSyntax> ClassDeclarations { get; } = new();

		public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
		{
			var node = context.Node;

			if (node is not ClassDeclarationSyntax c)
			{
				return;
			}

			var isValidClass =
				c.Identifier.Text.EndsWith("ViewModel") &&
				!Exclusions.Contains(c.Identifier.Text) &&
				!node.IsSourceGenerated();

			if (isValidClass)
			{
				ClassDeclarations.Add(c);
			}
		}
	}
}
