using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent;

public class ApplicationStateManager : IMainWindowService
{
	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
	private CompositeDisposable? _compositeDisposable;
	private bool _hideRequest;
	private bool _isShuttingDown;
	private bool _restartRequest;

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
			.OnTrigger(
			Trigger.ShutdownRequested,
			() =>
			{
				if (_restartRequest)
				{
					AppLifetimeHelper.StartAppWithArgs();
				}

				lifetime.Shutdown();
			})
			.OnTrigger(
			Trigger.ShutdownPrevented,
			() =>
			{
				ApplicationViewModel.OnShutdownPrevented(_restartRequest);
				_restartRequest = false; // reset the value.
			});

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

		if (result.WindowState != WindowState.Maximized)
		{
			SetWindowSize(result);
		}

		ObserveWindowSize(result, _compositeDisposable);

		result.Show();

		ApplicationViewModel.IsMainWindowShown = true;
	}

	private void SetWindowSize(Window window)
	{
		var configWidth = Services.UiConfig.WindowWidth;
		var configHeight = Services.UiConfig.WindowHeight;
		var currentScreen = window.Screens.ScreenFromPoint(window.Position);

		if (configWidth is null || configHeight is null || currentScreen is null)
		{
			return;
		}

		var isValidWidth = configWidth <= currentScreen.WorkingArea.Width && configWidth >= window.MinWidth;
		var isValidHeight = configHeight <= currentScreen.WorkingArea.Height && configHeight >= window.MinHeight;

		if (isValidWidth && isValidHeight)
		{
			window.Width = configWidth.Value;
			window.Height = configHeight.Value;
		}
	}

	private void ObserveWindowSize(Window window, CompositeDisposable disposables)
	{
		window
			.WhenAnyValue(x => x.Bounds)
			.Skip(1)
			.Where(b => !b.IsEmpty && window.WindowState == WindowState.Normal)
			.Subscribe(b =>
			{
				Services.UiConfig.WindowWidth = b.Width;
				Services.UiConfig.WindowHeight = b.Height;
			})
			.DisposeWith(disposables);
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

	void IMainWindowService.Shutdown(bool restart)
	{
		_restartRequest = restart;
		_stateMachine.Fire(ApplicationViewModel.CanShutdown() ? Trigger.ShutdownRequested : Trigger.ShutdownPrevented);
	}
}
