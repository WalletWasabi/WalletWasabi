using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.State;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Views;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent;

public class ApplicationStateManager : IMainWindowService
{
	enum Trigger
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
		BackgroundModeOn
	}

	enum State
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

	internal ApplicationStateManager(IClassicDesktopStyleApplicationLifetime lifetime, bool startInBg)
	{
		_lifetime = lifetime;

		_stateMachine =
			new StateMachine<State, Trigger>(Services.UiConfig.HideOnClose ? State.BackgroundMode : State.StandardMode);

		_stateMachine.Configure(State.BackgroundMode)
			.OnEntry(() =>
			{
				_stateMachine.Fire(Trigger.Initialise);
			})
			.OnTrigger(Trigger.ShutdownRequested, () =>
			{
				lifetime.Shutdown();
			})
			.OnTrigger(Trigger.ShutdownPrevented, () =>
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(new ShowErrorDialogViewModel (
						"Wasabi is currently anonymising your wallet. Please try again in a few minutes.",
						"Warning",
						"Unable to close right now"));
				});
			})
			.Permit(Trigger.BackgroundModeOff, State.StandardMode)
			.Permit(Trigger.Initialise, State.Closed);

		_stateMachine.Configure(State.Closed)
			.SubstateOf(State.BackgroundMode)
			.OnEntry(() =>
			{
				_lifetime.MainWindow = null;

				if (ApplicationViewModel is { })
				{
					ApplicationViewModel.IsMainWindowShown = false;
				}
			})
			.Permit(Trigger.Show, State.Open)
			.Permit(Trigger.ShutdownPrevented, State.Open)
			.Permit(Trigger.Loaded, State.Open);

		_stateMachine.Configure(State.Open)
			.SubstateOf(State.BackgroundMode)
			.OnEntry(CreateAndShowMainWindow)
			.OnTrigger(Trigger.Hide, () =>
			{
				_lifetime.MainWindow.Close();
			})
			.Permit(Trigger.Hide, State.Closed)
			.Permit(Trigger.MainWindowClosed, State.Closed);

		_stateMachine.Configure(State.StandardMode)
			.OnEntry(() =>
			{
				_stateMachine.Fire(Trigger.Initialise);
			})
			.OnTrigger(Trigger.ShutdownRequested, () =>
			{
				lifetime.Shutdown();
			})
			.OnExit(() =>
			{
				_lifetime.MainWindow.Closing -= MainWindowOnClosing;
			})
			.Permit(Trigger.BackgroundModeOn, State.BackgroundMode)
			.Permit(Trigger.Initialise, State.Shown);

		_stateMachine.Configure(State.Shown)
			.SubstateOf(State.StandardMode)
			.Permit(Trigger.Hide, State.Hidden)
			.OnEntry(() =>
			{
				if (_lifetime.MainWindow is null)
				{
					CreateAndShowMainWindow();

					_lifetime.MainWindow!.Closing += MainWindowOnClosing;
				}
				else
				{
					_lifetime.MainWindow.WindowState = WindowState.Normal;
				}

				if (ApplicationViewModel is { })
				{
					ApplicationViewModel.IsMainWindowShown = false;
				}
			});

		_stateMachine.Configure(State.Hidden)
			.SubstateOf(State.StandardMode)
			.Permit(Trigger.Show, State.Shown)
			.OnEntry(() =>
			{
				_lifetime.MainWindow.WindowState = WindowState.Minimized;

				if (ApplicationViewModel is { })
				{
					ApplicationViewModel.IsMainWindowShown = false;
				}
			});


		_lifetime.ShutdownRequested += LifetimeOnShutdownRequested;

		Services.UiConfig.WhenAnyValue(x => x.HideOnClose)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(backgroundMode =>
			{
				if (backgroundMode)
				{
					_stateMachine.Fire(Trigger.BackgroundModeOn);
				}
				else
				{
					_stateMachine.Fire(Trigger.BackgroundModeOff);
				}
			});

		_stateMachine.Start();

		if (!startInBg)
		{
			_stateMachine.Fire(Trigger.Loaded);
		}
	}

	private void MainWindowOnClosing(object? sender, CancelEventArgs e)
	{
		if (ApplicationViewModel is { })
		{
			e.Cancel = !ApplicationViewModel.CanShutdown();
		}

		if (e.Cancel)
		{
			_stateMachine.Fire(Trigger.ShutdownPrevented);
		}
		else
		{
			_stateMachine.Fire(Trigger.ShutdownRequested);
		}
	}

	private void LifetimeOnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
	{
		if (ApplicationViewModel is { })
		{
			// Shutdown prevention will only work if you directly run the executable.
			e.Cancel = !ApplicationViewModel.CanShutdown();

			Logger.LogDebug($"Cancellation of the shutdown set to: {e.Cancel}.");
		}

		if (e.Cancel)
		{
			_stateMachine.Fire(Trigger.ShutdownPrevented);
		}
		else
		{
			_stateMachine.Fire(Trigger.ShutdownRequested);
		}
	}

	private void CreateAndShowMainWindow()
	{
		var result = new MainWindow
		{
			DataContext = MainViewModel.Instance
		};

		Observable.FromEventPattern(result, nameof(result.Closed))
			.Take(1)
			.Subscribe(x =>
			{
				_stateMachine.Fire(Trigger.MainWindowClosed);
			});

		_lifetime.MainWindow = result;

		result.Show();

		if (ApplicationViewModel is { })
		{
			ApplicationViewModel.IsMainWindowShown = true;
		}
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
		if (ApplicationViewModel is { })
		{
			if (ApplicationViewModel.CanShutdown())
			{
				_stateMachine.Fire(Trigger.ShutdownRequested);
			}
			else
			{
				_stateMachine.Fire(Trigger.ShutdownPrevented);
			}
		}
	}

	internal ApplicationViewModel? ApplicationViewModel { get; set; }
}