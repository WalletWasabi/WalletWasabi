using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using System;

namespace WalletWasabi.Fluent.Generators;

internal abstract class GeneratorStep
{
	private readonly object _lock = new();

	public GeneratorStepContext Context { get; private set; }

	public void Initialize(GeneratorExecutionContext context, Compilation compilation)
	{
		Context = new GeneratorStepContext(context, compilation);
	}

	public virtual IEnumerable<(string FileName, string Source)> GenerateStaticFiles() => Array.Empty<(string, string)>();

	public virtual void OnInitialize(Compilation compilation, GeneratorStep[] steps)
	{
	}

	public abstract void Execute();

	protected SyntaxTree AddSource(string name, string source)
	{
		var syntaxTree = SyntaxFactory.ParseSyntaxTree(source);
		Context.Context.AddSource(name, SourceText.From(source, Encoding.UTF8));

		lock (_lock)
		{
			Context = Context with { Compilation = Context.Compilation.AddSyntaxTrees(syntaxTree) };
		}

		return syntaxTree;
	}

	protected void ReportDiagnostic(DiagnosticDescriptor diagnosticDescriptor, Location? location)
	{
		Context.Context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, location));
	}

	protected SemanticModel GetSemanticModel(SyntaxTree syntaxTree) => Context.Compilation.GetSemanticModel(syntaxTree);
}
