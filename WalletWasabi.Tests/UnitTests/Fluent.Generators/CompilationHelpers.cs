using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WalletWasabi.Tests.UnitTests.Fluent.Generators;

internal static class CompilationHelpers
{
	public static Compilation CreateCompilation(string source)
	{
		SyntaxTree[] syntaxTrees = new[] { CSharpSyntaxTree.ParseText(source) };
		CSharpCompilationOptions options = new(OutputKind.ConsoleApplication);
		PortableExecutableReference[] references = new[]
		{
			MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location),
			MetadataReference.CreateFromFile(typeof(ReactiveUI.ReactiveObject).GetTypeInfo().Assembly.Location),
		};

		return CSharpCompilation.Create("compilation", syntaxTrees, references, options);
	}
}