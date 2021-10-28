using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;

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

			Services.SingleInstanceChecker.OtherInstanceStarted += SingleInstanceCheckerOnOtherInstanceStarted;

			if (!Design.IsDesignMode)
			{
				MainViewModel.Instance = new MainViewModel();

				if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
				{
					desktopLifetime.ShutdownMode = Services.UiConfig.HideOnClose ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnMainWindowClose;

					desktopLifetime.MainWindow = new MainWindow
					{
						DataContext = MainViewModel.Instance
					};

					desktopLifetime.ShutdownRequested += DesktopOnShutdownRequested;

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

		private void ShowMainWindow()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
			{
				if (Services.UiConfig.HideOnClose && desktopLifetime.MainWindow is null)
				{
					desktopLifetime.MainWindow = new MainWindow
					{
						DataContext = MainViewModel.Instance
					};

					desktopLifetime.MainWindow.Show();
				}
				else if (!Services.UiConfig.HideOnClose && desktopLifetime.MainWindow is { } && desktopLifetime.MainWindow.WindowState == WindowState.Minimized)
				{
					if (desktopLifetime.MainWindow.WindowState == WindowState.Minimized)
					{
						desktopLifetime.MainWindow.WindowState = (WindowState)Enum.Parse(typeof(WindowState), Services.UiConfig.WindowState);
					}

					desktopLifetime.MainWindow.BringIntoView();
				}
			}
		}

		private void DesktopOnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
		{
			if (CanShutdownProvider is { } provider)
			{
				// Shutdown prevention will only work if you directly run the executable.
				e.Cancel = !provider.CanShutdown();
				Logger.LogDebug($"Cancellation of the shutdown set to: {e.Cancel}.");
			}
		}

		// Note, this is only supported on Win32 and some Linux DEs
		// https://github.com/AvaloniaUI/Avalonia/blob/99d983499f5412febf07aafe2bf03872319b412b/src/Avalonia.Controls/TrayIcon.cs#L66
		private void TrayIcon_OnClicked(object? sender, EventArgs e)
		{
			ShowMainWindow();
		}

		private void SingleInstanceCheckerOnOtherInstanceStarted(object? sender, EventArgs e)
		{
			ShowMainWindow();
		}
	}
}
