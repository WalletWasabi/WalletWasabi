using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui
{
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
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow()
				{
					DataContext = MainWindowViewModel.Instance
				};
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
