using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Fluent.Generators;

internal abstract class CombinedGenerator : ISourceGenerator
{
	private List<Func<GeneratorStep>> StepFactories { get; } = new();

	public void Initialize(GeneratorInitializationContext context)
	{
		foreach (var factory in StepFactories)
		{
			var step = factory();
			foreach (var (fileName, source) in step.GenerateStaticFiles())
			{
				context.RegisterForPostInitialization(ctx => ctx.AddSource(fileName, SourceText.From(source, Encoding.UTF8)));
			}
		}
		context.RegisterForSyntaxNotifications(() => new SyntaxReceiver(this));
	}

	public void Execute(GeneratorExecutionContext context)
	{
		if (context.SyntaxReceiver is not SyntaxReceiver receiver)
		{
			return;
		}

		var compilation = context.Compilation;

		foreach (var step in receiver.Steps)
		{
			step.Initialize(context, compilation);
			OnInitialize(step, compilation, receiver.Steps);
			step.Execute();

			// This is the core part of CombinedGenerator.
			// Each step creates a new Compilation, containing additional syntax trees
			// The CombinedGenerator passes the new Compilation (with the added Syntax Trees) from the step to step
			// in order to be able to semantically analyze the types declared inside those syntax trees
			// otherwise SemanticModel.GetTypeInfo() returns null or errored out type symbols for types
			// declared in source generated files.
			compilation = step.Context.Compilation;
		}
	}

	protected void Add<T>() where T : GeneratorStep, new()
	{
		StepFactories.Add(() => new T());
	}

	protected virtual void OnInitialize(GeneratorStep step, Compilation compilation, GeneratorStep[] steps)
	{
	}

	private class SyntaxReceiver : ISyntaxReceiver
	{
		public SyntaxReceiver(CombinedGenerator generator)
		{
			Steps = generator.StepFactories.Select(x => x()).ToArray();
		}

		public GeneratorStep[] Steps { get; }

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			foreach (var step in Steps)
			{
				step.OnVisitSyntaxNode(syntaxNode);
			}
		}
	}
}
