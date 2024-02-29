using Microsoft.CodeAnalysis;

namespace WalletWasabi.Fluent.Generators.Abstractions;

internal record GeneratorStepContext(GeneratorExecutionContext Context, Compilation Compilation);
