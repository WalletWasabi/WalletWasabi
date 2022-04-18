using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent;



public class App : Application, IMainWindowService
{
	private readonly Func<Task>? _backendInitialiseAsync;

	/// <summary>
	/// Defines the <see cref="CanShutdownProvider"/> property.
	/// </summary>/
	public static readonly StyledProperty<ICanShutdownProvider?> CanShutdownProviderProperty =
		AvaloniaProperty.Register<App, ICanShutdownProvider?>(nameof(CanShutdownProvider), null, defaultBindingMode: BindingMode.TwoWay);

	public App()
	{
		Name = "Wasabi Wallet";

		if (!Design.IsDesignMode)
		{
			DataContext = new ApplicationViewModel(this);
		}
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
		if (!Design.IsDesignMode)
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

				desktop.ShutdownRequested += DesktopOnShutdownRequested;

				desktop.MainWindow = CreateMainWindow();

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						await _backendInitialiseAsync!(); // Guaranteed not to be null when not in designer.

						MainViewModel.Instance.Initialize();
					});
			}
		}

		base.OnFrameworkInitializationCompleted();
	}

	private Window CreateMainWindow()
	{
		var result = new MainWindow
		{
			DataContext = MainViewModel.Instance
		};

		Observable.FromEventPattern(result, nameof(result.Closed))
			.Take(1)
			.Subscribe(x =>
			{
				if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is { })
				{
					desktop.MainWindow = null;

					if (DataContext is ApplicationViewModel avm)
					{
						avm.IsMainWindowShown = false;
					}
				}
			});

		return result;
	}

	private void DesktopOnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
	{
		if (CanShutdownProvider is { } provider)
		{
			// Shutdown prevention will only work if you directly run the executable.
			e.Cancel = !provider.CanShutdown();

			if (e.Cancel && DataContext is ApplicationViewModel avm)
			{
				avm.OnClosePrevented();
			}

			Logger.LogDebug($"Cancellation of the shutdown set to: {e.Cancel}.");
		}
	}

	void IMainWindowService.Show()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is null)
		{
			desktop.MainWindow = CreateMainWindow();
			desktop.MainWindow.Show();
		}
	}

	void IMainWindowService.Close()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is { })
		{
			desktop.MainWindow.Close();
			desktop.MainWindow = null;
		}
	}
}
