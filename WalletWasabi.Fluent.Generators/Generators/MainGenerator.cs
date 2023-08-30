using Microsoft.CodeAnalysis;

namespace WalletWasabi.Fluent.Generators;

[Generator]
internal class MainGenerator : CombinedGenerator
{
	public MainGenerator()
	{
		Add<UiContextConstructorGenerator>();
		Add<FluentNavigationGenerator>();
	}
}
