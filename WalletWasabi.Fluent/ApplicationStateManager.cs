using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent;

public class ApplicationStateManager : IMainWindowService
{
	private enum Trigger
	{
		Invalid = 0,
		Initialise,
		Hide,
		Show,
		Loaded,
		ShutdownPrevented,
		ShutdownRequested,
		MainWindowClosed,
		BackgroundModeOff,
		BackgroundModeOn,
		Minimised,
		Restored
	}

	private enum State
	{
		Invalid = 0,
		BackgroundMode,
		Closed,
		Open,
		StandardMode,
		Hidden,
		Shown
	}

	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
	private CompositeDisposable? _compositeDisposable;

	internal ApplicationStateManager(IClassicDesktopStyleApplicationLifetime lifetime, bool startInBg)
	{
		_lifetime = lifetime;
		_stateMachine = new StateMachine<State, Trigger>(Services.UiConfig.HideOnClose ? State.BackgroundMode : State.StandardMode);
		ApplicationViewModel = new ApplicationViewModel(this);

		_stateMachine.Configure(State.BackgroundMode)
			.OnEntry(() => _stateMachine.Fire(Trigger.Initialise))
			.OnTrigger(Trigger.ShutdownRequested, () => lifetime.Shutdown())
			.OnTrigger(Trigger.ShutdownPrevented, () => ApplicationViewModel.OnShutdownPrevented())
			.Permit(Trigger.BackgroundModeOff, State.StandardMode)
			.Permit(Trigger.Initialise, State.Closed);

		_stateMachine.Configure(State.Closed)
			.SubstateOf(State.BackgroundMode)
			.OnEntry(() =>
			{
				_lifetime.MainWindow = null;
				ApplicationViewModel.IsMainWindowShown = false;
			})
			.Permit(Trigger.Show, State.Open)
			.Permit(Trigger.ShutdownPrevented, State.Open)
			.Permit(Trigger.Loaded, State.Open);

		_stateMachine.Configure(State.Open)
			.SubstateOf(State.BackgroundMode)
			.OnEntry(CreateAndShowMainWindow)
			.OnTrigger(Trigger.Hide, () => _lifetime.MainWindow.Close())
			.Permit(Trigger.Hide, State.Closed)
			.Permit(Trigger.MainWindowClosed, State.Closed);

		_stateMachine.Configure(State.StandardMode)
			.OnEntry(() => _stateMachine.Fire(Trigger.Initialise))
			.OnTrigger(Trigger.ShutdownPrevented, () => ApplicationViewModel.OnShutdownPrevented())
			.OnTrigger(Trigger.ShutdownRequested, () => lifetime.Shutdown())
			.Permit(Trigger.BackgroundModeOn, State.BackgroundMode)
			.Permit(Trigger.Initialise, State.Shown);

		_stateMachine.Configure(State.Shown)
			.SubstateOf(State.StandardMode)
			.Permit(Trigger.Hide, State.Hidden)
			.Permit(Trigger.Minimised, State.Hidden)
			.OnEntry(() =>
			{
				if (_lifetime.MainWindow is null)
				{
					CreateAndShowMainWindow();
				}
				else if (_lifetime.MainWindow.WindowState == WindowState.Minimized)
				{
					_lifetime.MainWindow.WindowState = WindowState.Normal;
				}

				ApplicationViewModel.IsMainWindowShown = true;
			});

		_stateMachine.Configure(State.Hidden)
			.SubstateOf(State.StandardMode)
			.Permit(Trigger.Show, State.Shown)
			.Permit(Trigger.Restored, State.Shown)
			.Permit(Trigger.ShutdownPrevented, State.Shown)
			.OnEntry(() =>
			{
				_lifetime.MainWindow.WindowState = WindowState.Minimized;
				ApplicationViewModel.IsMainWindowShown = false;
			});

		_lifetime.ShutdownRequested += LifetimeOnShutdownRequested;

		Services.UiConfig.WhenAnyValue(x => x.HideOnClose)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(backgroundMode => _stateMachine.Fire(backgroundMode ? Trigger.BackgroundModeOn : Trigger.BackgroundModeOff));

		_stateMachine.Start();

		if (!startInBg)
		{
			_stateMachine.Fire(Trigger.Loaded);
		}

	}

	internal ApplicationViewModel ApplicationViewModel { get; }

	private void MainWindowOnClosing(object? sender, CancelEventArgs e)
	{
		if (_stateMachine.IsInState(State.StandardMode))
		{
			e.Cancel = !ApplicationViewModel.CanShutdown();

			if (e.Cancel)
			{
				_stateMachine.Fire(Trigger.ShutdownPrevented);
			}
			else if (sender is MainWindow w)
			{
				w.Closing -= MainWindowOnClosing;
				_stateMachine.Fire(Trigger.ShutdownRequested);
			}
		}
	}

	private void LifetimeOnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
	{
		// Shutdown prevention will only work if you directly run the executable.
		e.Cancel = !ApplicationViewModel.CanShutdown();

		Logger.LogDebug($"Cancellation of the shutdown set to: {e.Cancel}.");

		_stateMachine.Fire(e.Cancel ? Trigger.ShutdownPrevented : Trigger.ShutdownRequested);
	}

	private void CreateAndShowMainWindow()
	{
		if (_lifetime.MainWindow is { })
		{
			return;
		}

		var result = new MainWindow
		{
			DataContext = MainViewModel.Instance
		};

		result.Closing += MainWindowOnClosing;

		_compositeDisposable?.Dispose();
		_compositeDisposable = new();

		result.WhenAnyValue(x => x.WindowState)
			.Subscribe(windowState => _stateMachine.Fire(windowState == WindowState.Minimized ? Trigger.Minimised : Trigger.Restored))
			.DisposeWith(_compositeDisposable);

		Observable.FromEventPattern(result, nameof(result.Closed))
			.Take(1)
			.Subscribe(x =>
			{
				_compositeDisposable?.Dispose();
				_compositeDisposable = null;
				result.Closing -= MainWindowOnClosing;
				_stateMachine.Fire(Trigger.MainWindowClosed);
			})
			.DisposeWith(_compositeDisposable);

		_lifetime.MainWindow = result;

		result.Show();

		ApplicationViewModel.IsMainWindowShown = true;
	}

	void IMainWindowService.Show()
	{
		_stateMachine.Fire(Trigger.Show);
	}

	void IMainWindowService.Close()
	{
		_stateMachine.Fire(Trigger.Hide);
	}

	void IMainWindowService.Shutdown()
	{
		_stateMachine.Fire(ApplicationViewModel.CanShutdown() ? Trigger.ShutdownRequested : Trigger.ShutdownPrevented);
	}
}
