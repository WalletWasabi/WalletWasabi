using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using Global = WalletWasabi.Gui.Global;

namespace WalletWasabi.Fluent
{
	public class App : Application
	{
		private readonly Global? _global;
		private Func<Task> _backendInitialiseAsync;

		public App()
		{
			Name = "Wasabi Wallet";
		}

		public App(Global global, Func<Task> backendInitialiseAsync) : this()
		{
			_global = global;
			_backendInitialiseAsync = backendInitialiseAsync;
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
				MainViewModel.Instance = new MainViewModel(_global!);

				if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
				{
					desktop.MainWindow = new MainWindow
					{
						DataContext = MainViewModel.Instance
					};

					RxApp.MainThreadScheduler.Schedule(
						async () =>
						{
							await _backendInitialiseAsync();

							MainViewModel.Instance!.Initialize();
						});
				}
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}