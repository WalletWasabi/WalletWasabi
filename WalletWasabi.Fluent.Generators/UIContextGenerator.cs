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

// QuickFix for https://github.com/dotnet/roslyn-analyzers/issues/6467
#pragma warning disable RS1035

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

		var constructors = ProcessViewModels(context, receiver.ClassDeclarations)
			.OrderBy(x => x.Identifier.ValueText)
			.ToArray();

		GenerateFluentNavigation(context, constructors);
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

			var constructors = GenerateConstructors(context, cls, model, classSymbol).ToArray();

			result.AddRange(constructors);
		}

		return result;
	}

	private static IEnumerable<ConstructorDeclarationSyntax> GenerateConstructors(GeneratorExecutionContext context, ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel, INamedTypeSymbol classSymbol)
	{
		var fileName = classDeclaration.Identifier.ValueText + UiContextAnalyzer.UiContextFileSuffix;

		var className = classSymbol.Name;
		var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

		var constructors = classDeclaration
			.ChildNodes()
			.OfType<ConstructorDeclarationSyntax>()
			.ToArray();

		foreach (var constructor in constructors)
		{
			if (!classDeclaration.GetUiContextReferences(semanticModel).Any())
			{
				if (!classDeclaration.IsAbstractClass(semanticModel) && constructor.IsPublic())
				{
					yield return constructor;
				}
			}
			// if constructor already has a UIContext parameter, leave it be. Don't generate a new constructor and use the current one for FluentNavigation.
			else if (constructor.ParameterList.Parameters.Any(p => p.Type.IsUiContextType(semanticModel)))
			{
				// it must be public though
				if (constructor.IsPublic())
				{
					yield return constructor;
				}
			}
			else
			{
				var constructorArgs = constructor.ParameterList.Parameters
					.Select(x => x.Identifier.ValueText)
					.ToArray();

				var hasConstructorArgs = constructorArgs.Any();
				var constructorArgsString = string.Join(",", constructorArgs);
				var constructorString = hasConstructorArgs
					? $": this({constructorArgsString})"
					: $": this()";

				var parameterUsings = constructor.ParameterList.Parameters
					.Where(p => p.Type is not null)
					.Select(p => semanticModel.GetTypeInfo(p.Type!))
					.Where(t => t.Type is not null)
					.Select(t => $"using {t.Type!.ContainingNamespace.ToDisplayString()};")
					.ToArray();

				var uiContextParameter = SyntaxFactory
					.Parameter(SyntaxFactory.Identifier("uiContext").WithLeadingTrivia(SyntaxFactory.Space))
					.WithType(SyntaxFactory.ParseTypeName("UiContext"));

				var parametersString = constructor.ParameterList.Parameters.Insert(0, uiContextParameter).ToFullString();

				var usings = string.Join(Environment.NewLine, parameterUsings.Distinct().OrderBy(x => x));

				var code =
					$$"""
					{{usings}}
					using WalletWasabi.Fluent.Models.UI;

					namespace {{namespaceName}};

					partial class {{className}}
					{
						public {{className}}({{parametersString}}){{constructorString}}
						{
							UiContext = uiContext;
						}
					}
					""";

				var sourceText = SourceText.From(code, Encoding.UTF8);
				context.AddSource(fileName, sourceText);

				var tree = CSharpSyntaxTree.ParseText(sourceText, context.Compilation.SyntaxTrees.First().Options as CSharpParseOptions);

				var newConstructor = tree
					.GetRoot()
					.DescendantNodes()
					.OfType<ConstructorDeclarationSyntax>()
					.First();

				yield return newConstructor;
			}
		}
	}

	private static void GenerateFluentNavigation(GeneratorExecutionContext context, IEnumerable<ConstructorDeclarationSyntax> constructors)
	{
		var compilation = context.Compilation;
		var namespaces = new List<string>();
		var methods = new List<string>();

		var newSyntaxTrees = constructors
			.Select(x => x.SyntaxTree)
			.Where(x => !compilation.ContainsSyntaxTree(x))
			.ToArray();

		compilation = compilation.AddSyntaxTrees(newSyntaxTrees);

		foreach (var constructor in constructors)
		{
			var semanticModel = compilation.GetSemanticModel(constructor.SyntaxTree);

			if (constructor.Parent is not ClassDeclarationSyntax cls)
			{
				continue;
			}

			if (!cls.IsRoutableViewModel(semanticModel))
			{
				continue;
			}

			if (cls.IsAbstractClass(semanticModel))
			{
				continue;
			}

			var viewModelTypeInfo =	semanticModel.GetDeclaredSymbol(cls);

			if (viewModelTypeInfo == null)
			{
				continue;
			}

			var className = cls.Identifier.ValueText;

			var constructorNamespaces = constructor.ParameterList.Parameters
				.Where(p => p.Type is not null)
				.Select(p => semanticModel.GetTypeInfo(p.Type!))
				.Where(t => t.Type is not null)
				.SelectMany(t => t.Type.GetNamespaces())
				.ToArray();

			var uiContextParam = constructor.ParameterList.Parameters.FirstOrDefault(x => x.Type.IsUiContextType(semanticModel));

			var methodParams = constructor.ParameterList;

			if (uiContextParam != null)
			{
				methodParams = SyntaxFactory.ParameterList(methodParams.Parameters.Remove(uiContextParam));
			}

			var navigationMetadata = viewModelTypeInfo
				.GetAttributes()
				.FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == NavigationMetaDataGenerator.NavigationMetaDataAttributeDisplayString);

			var defaultNavigationTarget = "DialogScreen";

			if (navigationMetadata != null)
			{
				var navigationArgument = navigationMetadata.NamedArguments
					.FirstOrDefault(x => x.Key == "NavigationTarget");

				if (navigationArgument.Value.Type is INamedTypeSymbol navigationTargetEnum)
				{
					var enumValue = navigationTargetEnum
						.GetMembers()
						.OfType<IFieldSymbol>()
						.FirstOrDefault(x => x.ConstantValue?.Equals(navigationArgument.Value.Value) == true);

					if (enumValue != null)
					{
						defaultNavigationTarget = enumValue.Name;
					}
				}
			}

			var additionalMethodParams =
				$"NavigationTarget navigationTarget = NavigationTarget.{defaultNavigationTarget}, NavigationMode navigationMode = NavigationMode.Normal";

			methodParams = methodParams.AddParameters(SyntaxFactory.ParseParameterList(additionalMethodParams).Parameters.ToArray());

			var constructorArgs =
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SeparatedList(
					constructor.ParameterList
						.Parameters
						.Select(x => x.Type.IsUiContextType(semanticModel) ? "UiContext" : x.Identifier.ValueText) // replace uiContext argument for UiContext property reference
						.Select(x => SyntaxFactory.ParseExpression(x))
						.Select(SyntaxFactory.Argument),
					constructor.ParameterList
						.Parameters
						.Skip(1)
						.Select(x => SyntaxFactory.Token(SyntaxKind.CommaToken))));

			namespaces.Add(viewModelTypeInfo.ContainingNamespace.ToDisplayString());
			namespaces.AddRange(constructorNamespaces);

			var methodName = className.Replace("ViewModel", "");

			var (dialogReturnType, dialogReturnTypeNamespace) = cls.GetDialogResultType(semanticModel);

			foreach (var ns in dialogReturnTypeNamespace)
			{
				namespaces.Add(ns);
			}

			if (dialogReturnType is { })
			{
				var dialogString =
					$$"""
						public FluentDialog<{{dialogReturnType}}> {{methodName}}{{methodParams}}
						{
						    var dialog = new {{className}}{{constructorArgs.ToFullString()}};
							var target = UiContext.Navigate(navigationTarget);
							target.To(dialog, navigationMode);

							return new FluentDialog<{{dialogReturnType}}>(target.NavigateDialogAsync(dialog, navigationMode));
						}

					""";
				methods.Add(dialogString);
			}
			else
			{
				var methodString =
				$$"""
					public void {{methodName}}{{methodParams}}
					{
						UiContext.Navigate(navigationTarget).To(new {{className}}{{constructorArgs.ToFullString()}}, navigationMode);
					}

				""";
				methods.Add(methodString);
			}
		}

		var usings = namespaces
			.Distinct()
			.OrderBy(x => x)
			.Select(n => $"using {n};")
			.ToArray();

		var usingsString = string.Join(Environment.NewLine, usings);

		var methodsString = string.Join(Environment.NewLine, methods);

		var sourceText =
			$$"""
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
