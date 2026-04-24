using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Discover Coordinators", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class DiscoverCoordinatorsDialogViewModel : DialogViewModelBase<Uri?>
{
	[AutoNotify] private KnownCoordinatorItem? _selectedCoordinator;

	public DiscoverCoordinatorsDialogViewModel(Network network, Uri? currentCoordinator)
	{
		Coordinators = Services.Status.KnownCoordinators
			.Where(c => c.Network == network)
			.Select(c => new KnownCoordinatorItem(c, IsCurrent(c.CoordinatorUri, currentCoordinator)))
			.ToList();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = true;

		var canUseSelected = this.WhenAnyValue(x => x.SelectedCoordinator).Select(x => x is not null);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, SelectedCoordinator?.Uri), canUseSelected);
	}

	public IReadOnlyList<KnownCoordinatorItem> Coordinators { get; }

	private static bool IsCurrent(Uri uri, Uri? currentCoordinator) =>
		currentCoordinator is not null &&
		Uri.Compare(uri, currentCoordinator, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
}

public class KnownCoordinatorItem
{
	public KnownCoordinatorItem(KnownCoordinator coordinator, bool isCurrentlyConnected)
	{
		Name = coordinator.Name;
		Uri = coordinator.CoordinatorUri;
		Description = coordinator.Description;
		ReadMoreUri = coordinator.ReadMoreUri;
		IsCurrentlyConnected = isCurrentlyConnected;
	}

	public string Name { get; }
	public Uri Uri { get; }
	public string UriString => Uri.ToString();
	public string Description { get; }
	public Uri? ReadMoreUri { get; }
	public bool HasReadMore => ReadMoreUri is not null;
	public bool IsCurrentlyConnected { get; }
}
