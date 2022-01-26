using System.Reactive.Concurrency;
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
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent;

public class App : Application
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
		ApplicationViewModel applicationViewModel = new();
		DataContext = applicationViewModel;
		applicationViewModel.ShowRequested += (sender, args) => ShowRequested?.Invoke(sender, args);
	}

	public App(Func<Task> backendInitialiseAsync) : this()
	{
		_backendInitialiseAsync = backendInitialiseAsync;
	}

	public event EventHandler? ShowRequested;

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
						await _backendInitialiseAsync!(); // Guaranteed not to be null when not in designer.

						MainViewModel.Instance.Initialize();
					});
			}
		}

		base.OnFrameworkInitializationCompleted();
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
		ShowRequested?.Invoke(this, EventArgs.Empty);
	}
}
