using Avalonia;

namespace WalletWasabi.Fluent;

public interface IWalletWasabiAppBuilder
{
	AppBuilder? AppBuilder { get; set; }

	AppBuilder SetupAppBuilder(AppBuilder appBuilder);
}
