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
	enum Trigger
	{
		Invalid = 0,
		Initialise,
		Hide,
		Show,
		Loaded,
		ShutdownPrevented,
		ShutdownRequested,
		MainWindowClosed
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

		_lifetime.ShutdownRequested += LifetimeOnShutdownRequested;

		_stateMachine.Start();

		if (!startInBg)
		{
			_stateMachine.Fire(Trigger.Loaded);
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

	internal ApplicationViewModel? ApplicationViewModel { get; set; }
}