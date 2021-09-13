using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
	{
		public ApplicationViewModel()
		{
			QuitCommand = ReactiveCommand.Create(() =>
			{
				if (CanShutdown())
				{
					if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
						desktopLifetime)
					{
						desktopLifetime.Shutdown();
					}
				}
			});
		}

		public ICommand QuitCommand { get; }

		public bool CanShutdown()
		{
			return Services.HostedServices.Get<CoinJoinManager>().HighestCoinJoinClientState switch
			{
				CoinJoinClientState.InCriticalPhase => false,
				CoinJoinClientState.Idle or CoinJoinClientState.InProgress => true,
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}
