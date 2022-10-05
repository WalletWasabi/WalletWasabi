using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Statistics;

[NavigationMetaData(
	Title = "Coinjoin Monitor",
	Caption = "Displays coinjoin monitoring",
	IconName = "info_regular",
	Order = 5,
	Category = "General",
	Keywords = new[] { "CoinJoin", "Monitor", "Arena", "WabiSabi", "Coordinator", "Statistics", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinMonitorViewModel : RoutableViewModel
{
	[AutoNotify(SetterModifier = AccessModifier.Private)] private List<RoundState>? _roundStates;

	public CoinJoinMonitorViewModel()
	{
		EnableBack = false;

		NextCommand = CancelCommand;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				RoundStates = GetRoundStates();
			});
	}

	private List<RoundState>? GetRoundStates()
	{
		// TODO: How to get same data as:
		// WabiSabiCoordinator.Arena.Rounds
		// WabiSabiController.GetHumanMonitor()

		var coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();
		if (coinJoinManager?.RoundStatusUpdater is { } roundStateUpdater)
		{
			return roundStateUpdater.GetRoundStates()
				.Where(x => x.Phase is not Phase.Ended)
				.Select(x => x)
				.ToList();
		}

		return null;
	}
}
