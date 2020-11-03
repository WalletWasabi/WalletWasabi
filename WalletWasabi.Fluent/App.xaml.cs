using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using Global = WalletWasabi.Gui.Global;

namespace WalletWasabi.Fluent
{
	public class App : Application
	{
		private Global _global;

		public App()
		{
			Name = "Wasabi Wallet";
		}

		public App(Global global) : this()
		{
			_global = global;
		}

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			AutoBringIntoViewExtension.Initialise();

			if (!Design.IsDesignMode)
			{
				MainViewModel.Instance = new MainViewModel(_global);

				if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
				{
					desktop.MainWindow = new MainWindow
					{
						DataContext = MainViewModel.Instance
					};
				}
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
