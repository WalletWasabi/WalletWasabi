using System;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;

namespace WalletWasabi.Fluent
{
	public class App : Application
	{
		private Func<Task> _backendInitialiseAsync;

		/// <summary>
		/// Defines the <see cref="CanShutdownProvider"/> property.
		/// </summary>/
		public static readonly StyledProperty<ICanShutdownProvider?> CanShutdownProviderProperty =
			AvaloniaProperty.Register<App, ICanShutdownProvider?>(nameof(CanShutdownProvider), null, defaultBindingMode: BindingMode.TwoWay);

		public App()
		{
			Name = "Wasabi Wallet";
		}

		public App(Func<Task> backendInitialiseAsync) : this()
		{
			_backendInitialiseAsync = backendInitialiseAsync;
		}

		public ICanShutdownProvider? CanShutdownProvider
		{
			get => GetValue(CanShutdownProviderProperty);
			set => SetValue(CanShutdownProviderProperty, value);
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
					desktop.ShutdownRequested += DesktopOnShutdownRequested;

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

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				Services.SingleInstanceChecker.OtherInstanceStarted += (sender, args) =>
				{
					MainViewModel.Instance!.WindowState = WindowState.Normal; // Todo: Unhide, Show, BringToFront?
				};
			}
		}

		private void DesktopOnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
		{
			if (CanShutdownProvider is { } provider)
			{
				bool hideOnClose = Services.UiConfig.HideOnClose;
				e.Cancel = !provider.CanShutdown() || hideOnClose;

				if (hideOnClose)
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						// Todo: hide the window and hide taskbar icon as well
						MainViewModel.Instance!.WindowState = WindowState.Minimized;
					}
					else
					{
						MainViewModel.Instance!.WindowState = WindowState.Minimized;
					}
				}
			}
		}
	}
}
