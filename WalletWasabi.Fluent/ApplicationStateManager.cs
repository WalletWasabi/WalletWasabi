using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls.ApplicationLifetimes;
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
		Hide,
		Show,
		Loaded,
		ShutdownPrevented,
		ShutdownRequested,
		MainWindowClosed,
	}

	private enum State
	{
		Invalid = 0,
		Closed,
		Open,
		InitialState
	}

	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
	private CompositeDisposable? _compositeDisposable;

	internal ApplicationStateManager(IClassicDesktopStyleApplicationLifetime lifetime, bool startInBg)
	{
		_lifetime = lifetime;
		_stateMachine = new StateMachine<State, Trigger>(State.InitialState);
		ApplicationViewModel = new ApplicationViewModel(this);

		_stateMachine.Configure(State.InitialState)
			.InitialTransition(State.Open)
			.OnTrigger(Trigger.ShutdownRequested, () => lifetime.Shutdown())
			.OnTrigger(Trigger.ShutdownPrevented, () => ApplicationViewModel.OnShutdownPrevented());

		_stateMachine.Configure(State.Closed)
			.SubstateOf(State.InitialState)
			.OnEntry(() =>
			{
				_lifetime.MainWindow.Close();
				_lifetime.MainWindow = null;
				ApplicationViewModel.IsMainWindowShown = false;
			})
			.Permit(Trigger.Show, State.Open)
			.Permit(Trigger.ShutdownPrevented, State.Open)
			.Permit(Trigger.Loaded, State.Open);

		_stateMachine.Configure(State.Open)
			.SubstateOf(State.InitialState)
			.OnEntry(CreateAndShowMainWindow)
			.Permit(Trigger.Hide, State.Closed)
			.Permit(Trigger.MainWindowClosed, State.Closed);

		_lifetime.ShutdownRequested += LifetimeOnShutdownRequested;

		_stateMachine.Start();

		if (!startInBg)
		{
			_stateMachine.Fire(Trigger.Loaded);
		}
	}

	internal ApplicationViewModel ApplicationViewModel { get; }

	private void MainWindowOnClosing(object? sender, CancelEventArgs e)
	{
		// TODO
		// if (_stateMachine.IsInState(State.StandardMode))
		// {
		// 	e.Cancel = !ApplicationViewModel.CanShutdown();
		//
		// 	if (e.Cancel)
		// 	{
		// 		_stateMachine.Fire(Trigger.ShutdownPrevented);
		// 	}
		// 	else if (sender is MainWindow w)
		// 	{
		// 		w.Closing -= MainWindowOnClosing;
		// 		_stateMachine.Fire(Trigger.ShutdownRequested);
		// 	}
		// }
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
