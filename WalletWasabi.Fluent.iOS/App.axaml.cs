using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.iOS;

public class App : Avalonia.Application
{
	public App()
	{
		Name = "Wasabi Wallet";
	}

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is ISingleViewApplicationLifetime single)
		{
			// TODO:
		}
	}
}
