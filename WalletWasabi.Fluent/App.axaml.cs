using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent;

public class App : Application
{
	private readonly Func<Task>? _backendInitialiseAsync;
	private ApplicationStateManager? _applicationStateManager;
	private readonly bool _startInBg;

	public App()
	{
		Name = "Wasabi Wallet";
	}

	public App(Func<Task> backendInitialiseAsync, bool startInBg) : this()
	{
		_startInBg = startInBg;
		_backendInitialiseAsync = backendInitialiseAsync;
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
				_applicationStateManager =
					new ApplicationStateManager(desktop, _startInBg);

				DataContext = _applicationStateManager.ApplicationViewModel;

				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
}
