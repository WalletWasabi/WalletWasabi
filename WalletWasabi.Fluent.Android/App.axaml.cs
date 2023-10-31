using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Android;

public class App : Application
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
