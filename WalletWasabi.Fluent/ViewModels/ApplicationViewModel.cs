using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
	{
		public ApplicationViewModel()
		{
			QuitCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (CanShutdown())
				{
					if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
						desktopLifetime)
					{
						desktopLifetime.Shutdown();
					}
				}
				else
				{
					// Show the window if it was hidden.
					ShowRequested?.Invoke(this, EventArgs.Empty);

					await MainViewModel.Instance!.CompactDialogScreen.NavigateDialogAsync(new Dialogs.ShowErrorDialogViewModel(
						"Wasabi is currently anonymising your wallet. Please try again in a few seconds.",
						"Warning",
						"Unable to close"));
				}
			});

			ShowCommand = ReactiveCommand.Create(() => ShowRequested?.Invoke(this, EventArgs.Empty));
		}

		public event EventHandler? ShowRequested;

		public ICommand QuitCommand { get; }
		public ICommand ShowCommand { get; }

		public bool CanShutdown()
		{
			var cjManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

			if (cjManager is { })
			{
				return cjManager.HighestCoinJoinClientState switch
				{
					CoinJoinClientState.InCriticalPhase => false,
					CoinJoinClientState.Idle or CoinJoinClientState.InProgress => true,
					_ => throw new ArgumentOutOfRangeException(),
				};
			}

			return true;
		}
	}
}
