using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;

namespace WalletWasabi.Fluent
{
	public class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow
				{
					DataContext = new MainWindowViewModel(),
				};
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}