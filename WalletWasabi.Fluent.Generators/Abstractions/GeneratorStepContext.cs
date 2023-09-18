using Microsoft.CodeAnalysis;

namespace WalletWasabi.Fluent.Generators;

internal record GeneratorStepContext(GeneratorExecutionContext Context, Compilation Compilation);
