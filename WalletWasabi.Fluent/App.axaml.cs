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

namespace WalletWasabi.Fluent
{
	public class App : Application
	{
		private Func<Task> _backendInitialiseAsync;

		public App()
		{
			Name = "Wasabi Wallet";
		}

		public App(Func<Task> backendInitialiseAsync) : this()
		{
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
				MainViewModel.Instance = new MainViewModel();

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