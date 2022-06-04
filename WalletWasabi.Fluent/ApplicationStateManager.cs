using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;
using WalletWasabi.Services;

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
		InitialState,
		Closed,
		Open,
	}

	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
	private CompositeDisposable? _compositeDisposable;
	private bool _hideRequest;
	private bool _isShuttingDown;

	internal ApplicationStateManager(IClassicDesktopStyleApplicationLifetime lifetime, bool startInBg)
	{
		_lifetime = lifetime;
		_stateMachine = new StateMachine<State, Trigger>(State.InitialState);
		ApplicationViewModel = new ApplicationViewModel(this);

		Observable
			.FromEventPattern(Services.SingleInstanceChecker, nameof(SingleInstanceChecker.OtherInstanceStarted))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => _stateMachine.Fire(Trigger.Show));

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
			.Permit(Trigger.MainWindowClosed, State.Closed)
			.OnTrigger(Trigger.Show, MainViewModel.Instance.ApplyUiConfigWindowSate);

		_lifetime.ShutdownRequested += LifetimeOnShutdownRequested;

		_stateMachine.Start();

		if (!startInBg)
		{
			_stateMachine.Fire(Trigger.Loaded);
		}
	}

	internal ApplicationViewModel ApplicationViewModel { get; }

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

		_compositeDisposable?.Dispose();
		_compositeDisposable = new();

		Observable.FromEventPattern<CancelEventArgs>(result, nameof(result.Closing))
			.Select(args => (args.EventArgs, !ApplicationViewModel.CanShutdown()))
			.TakeWhile(_ => !_isShuttingDown) // Prevents stack overflow.
			.Subscribe(tup =>
			{
				// _hideRequest flag is used to distinguish what is the user's intent.
				// It is only true when the request comes from the Tray.
				if (Services.UiConfig.HideOnClose || _hideRequest)
				{
					_hideRequest = false; // request processed, set it back to the default.
					return;
				}

				var (e, preventShutdown) = tup;

				_isShuttingDown = !preventShutdown;
				e.Cancel = preventShutdown;
				_stateMachine.Fire(preventShutdown ? Trigger.ShutdownPrevented : Trigger.ShutdownRequested);
			})
			.DisposeWith(_compositeDisposable);

		Observable.FromEventPattern(result, nameof(result.Closed))
			.Take(1)
			.Subscribe(_ =>
			{
				_compositeDisposable?.Dispose();
				_compositeDisposable = null;
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

	void IMainWindowService.Hide()
	{
		_hideRequest = true;
		_stateMachine.Fire(Trigger.Hide);
	}

	void IMainWindowService.Shutdown()
	{
		_stateMachine.Fire(ApplicationViewModel.CanShutdown() ? Trigger.ShutdownRequested : Trigger.ShutdownPrevented);
	}
}
