using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using Avalonia.Threading;

namespace WalletWasabi.Fluent;

[AppLifetime]
public class ApplicationStateManager : IMainWindowService
{
	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly IApplicationLifetime _lifetime;
	private CompositeDisposable? _compositeDisposable;
	private bool _hideRequest;
	private bool _isShuttingDown;
	private bool _restartRequest;
	private IActivatableLifetime? _activatable;

	internal ApplicationStateManager(IApplicationLifetime lifetime, UiContext uiContext, bool startInBg)
	{
		_lifetime = lifetime;
		_stateMachine = new StateMachine<State, Trigger>(State.InitialState);

		var activatableLifetime = Application.Current?.TryGetFeature<IActivatableLifetime>();

		if (activatableLifetime is not null)
		{
			if (startInBg)
			{
				Dispatcher.UIThread.Post(
					() =>
					{
						_activatable = activatableLifetime;
						activatableLifetime.TryEnterBackground();
						activatableLifetime.Activated += ActivatableLifetimeOnActivated;
						activatableLifetime.Deactivated += ActivatableLifetimeOnDeactivated;
					},
					DispatcherPriority.Background);
			}
			else
			{
				_activatable = activatableLifetime;
				activatableLifetime.Activated += ActivatableLifetimeOnActivated;
				activatableLifetime.Deactivated += ActivatableLifetimeOnDeactivated;
			}
		}

		UiContext = uiContext;
		ApplicationViewModel = new ApplicationViewModel(UiContext, this);
		State initTransitionState = startInBg ? State.Closed : State.Open;

		Observable
			.FromEventPattern(Services.SingleInstanceChecker, nameof(SingleInstanceChecker.OtherInstanceStarted))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => _stateMachine.Fire(Trigger.Show));

		_stateMachine.Configure(State.InitialState)
			.InitialTransition(initTransitionState)
			.OnTrigger(
				Trigger.ShutdownRequested,
				() =>
				{
					if (_restartRequest)
					{
						AppLifetimeHelper.StartAppWithArgs();
					}

					if (_lifetime is IControlledApplicationLifetime controlled)
					{
						controlled.Shutdown();
					}
					else
					{
						// TODO: Support shutdown on Mobile.
					}
				})
			.OnTrigger(
				Trigger.ShutdownPrevented,
				() =>
				{
					if (_lifetime is IClassicDesktopStyleApplicationLifetime desktop)
					{
						desktop.MainWindow.BringToFront();
						ApplicationViewModel.OnShutdownPrevented(_restartRequest);
						_restartRequest = false; // reset the value.
					}
					else
					{
						// TODO: Support shutdown prevented on Mobile.
					}
				});

		_stateMachine.Configure(State.Closed)
			.SubstateOf(State.InitialState)
			.OnEntry(() =>
			{
				if (_lifetime is IClassicDesktopStyleApplicationLifetime desktop)
				{
					desktop.MainWindow?.Close();
					desktop.MainWindow = null;
				}
				else
				{
					// TODO: Support close on Mobile.
				}

				ApplicationViewModel.IsMainWindowShown = false;
				if (_activatable is { })
				{
					_activatable.TryEnterBackground();
				}
			})
			.Permit(Trigger.Show, State.Open)
			.Permit(Trigger.ShutdownPrevented, State.Open);

		_stateMachine.Configure(State.Open)
			.SubstateOf(State.InitialState)
			.OnEntry(CreateAndShowMainWindow)
			.Permit(Trigger.Hide, State.Closed)
			.Permit(Trigger.MainWindowClosed, State.Closed)
			.OnTrigger(Trigger.Show, MainViewModel.Instance.ApplyUiConfigWindowState);

		if (_lifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.ShutdownRequested += LifetimeOnShutdownRequested;
		}
		else
		{
			// TODO: Support shutdown requested on Mobile.
		}

		_stateMachine.Start();
	}

	private enum Trigger
	{
		Invalid = 0,
		Hide,
		Show,
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

	internal UiContext UiContext { get; }
	internal ApplicationViewModel ApplicationViewModel { get; }

	private void LifetimeOnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
	{
		// Shutdown prevention will only work if you directly run the executable.
		bool shouldShutdown = ApplicationViewModel.CanShutdown(_restartRequest, out bool isShutdownEnforced) || isShutdownEnforced;

		e.Cancel = !shouldShutdown;
		Logger.LogDebug($"Cancellation of the shutdown set to: {e.Cancel}.");

		_stateMachine.Fire(shouldShutdown ? Trigger.ShutdownRequested : Trigger.ShutdownPrevented);
	}

	private void ActivatableLifetimeOnActivated(object? sender, ActivatedEventArgs e)
	{
		switch (e.Kind)
		{
			case ActivationKind.Background:
			case ActivationKind.Reopen:
				if (this is IMainWindowService service)
				{
					service.Show();
				}
				break;
		}
	}

	private void ActivatableLifetimeOnDeactivated(object? sender, ActivatedEventArgs e)
	{
		switch (e.Kind)
		{
			case ActivationKind.Background:
				if (this is IMainWindowService service)
				{
					if (_lifetime is IClassicDesktopStyleApplicationLifetime desktop)
					{
						if (desktop.MainWindow is not null)
						{
							service.Hide();
						}
					}
					else
					{
						// TODO: Support ActivationKind.Background on Mobile.
					}
				}
				break;
		}
	}

	private void CreateAndShowMainWindow()
	{
		switch (_lifetime)
		{
			case IClassicDesktopStyleApplicationLifetime { MainWindow: not null }:
			case ISingleViewApplicationLifetime { MainView: not null }:
				return;
		}

		MainViewModel.Instance.ApplyUiConfigWindowState();

		if (_activatable is not null)
		{
			_activatable.TryLeaveBackground();
		}

		if (_lifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var result = new MainWindow
			{
				DataContext = MainViewModel.Instance
			};

			_compositeDisposable?.Dispose();
			_compositeDisposable = new();

			Observable.FromEventPattern<CancelEventArgs>(result, nameof(result.Closing))
				.Select(args => (args.EventArgs, !ApplicationViewModel.CanShutdown(false, out bool isShutdownEnforced), isShutdownEnforced))
				.TakeWhile(_ => !_isShuttingDown) // Prevents stack overflow.
				.Subscribe(tup =>
				{
					var (e, preventShutdown, isShutdownEnforced) = tup;

					// Check if Ctrl-C was used to forcefully terminate the app.
					if (isShutdownEnforced)
					{
						_isShuttingDown = true;
						tup.EventArgs.Cancel = false;
						_stateMachine.Fire(Trigger.ShutdownRequested);
					}

					// _hideRequest flag is used to distinguish what is the user's intent.
					// It is only true when the request comes from the Tray.
					if (Services.UiConfig.HideOnClose || _hideRequest)
					{
						_hideRequest = false; // request processed, set it back to the default.
						return;
					}

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

			desktop.MainWindow = result;

			if (result.WindowState != WindowState.Maximized)
			{
				SetWindowSize(result);
			}

			ObserveWindowSize(result, _compositeDisposable);

			result.Show();

			ApplicationViewModel.IsMainWindowShown = true;
		}
		else if (_lifetime is ISingleViewApplicationLifetime single)
		{
			var result = new MobileView
			{
				DataContext = MainViewModel.Instance
			};

			_compositeDisposable?.Dispose();
			_compositeDisposable = new();

			// TODO: Handle Closing event on mobile similar like Window.Closing event.
			// TODO: Handle Closed event on mobile similar like Window.Closed event.

			single.MainView = result;

			// TODO: We do not handle WindowState.Maximized or use ObserveWindowSize on mobile.

			// TODO: We do not use Show() on mobile like on desktop window.

			ApplicationViewModel.IsMainWindowShown = true;
		}
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
			.Where(b => b != default && window.WindowState == WindowState.Normal)
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

		bool shouldShutdown = ApplicationViewModel.CanShutdown(_restartRequest, out bool isShutdownEnforced) || isShutdownEnforced;
		_stateMachine.Fire(shouldShutdown ? Trigger.ShutdownRequested : Trigger.ShutdownPrevented);
	}
}
