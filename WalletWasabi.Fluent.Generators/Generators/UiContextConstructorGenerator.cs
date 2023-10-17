using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.Generators.Abstractions;
using WalletWasabi.Fluent.Generators.Analyzers;

namespace WalletWasabi.Fluent.Generators.Generators;

internal class UiContextConstructorGenerator : GeneratorStep<ClassDeclarationSyntax>
{
	public List<ConstructorDeclarationSyntax> Constructors { get; } = new();

	public override bool Filter(ClassDeclarationSyntax cls)
	{
		var exclusions = new[]
		{
			"RoutableViewModel"
		};

		return
			cls.Identifier.Text.EndsWith("ViewModel") &&
			!exclusions.Contains(cls.Identifier.Text) &&
			!cls.IsSourceGenerated();
	}

	public override void Execute(ClassDeclarationSyntax[] classDeclarations)
	{
		Constructors.Clear();

		var toGenerate =
			from cls in classDeclarations
			group cls by cls.Identifier.ValueText into g
			select g.First();

		foreach (var cls in toGenerate)
		{
			var model = GetSemanticModel(cls.SyntaxTree);

			if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol classSymbol)
			{
				continue;
			}

			var constructors = GenerateConstructors(cls, model, classSymbol).ToArray();

			Constructors.AddRange(constructors);
		}
	}

	private IEnumerable<ConstructorDeclarationSyntax> GenerateConstructors(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel, INamedTypeSymbol classSymbol)
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

				var usings = string.Join("\r\n", parameterUsings.Distinct().OrderBy(x => x));

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

				var syntaxTree = AddSource(fileName, code);

				var newConstructor = syntaxTree
					.GetRoot()
					.DescendantNodes()
					.OfType<ConstructorDeclarationSyntax>()
					.First();

				yield return newConstructor;
			}
		}
	}

	public override void OnInitialize(Compilation compilation, GeneratorStep[] steps)
	{
		var uiContextGenerator = steps.OfType<UiContextConstructorGenerator>().First();
		Constructors.Clear();
		Constructors.AddRange(uiContextGenerator.Constructors);
	}
}
