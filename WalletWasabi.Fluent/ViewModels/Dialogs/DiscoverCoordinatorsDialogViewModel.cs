using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Discover Coordinators", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class DiscoverCoordinatorsDialogViewModel : DialogViewModelBase<Uri?>
{
	private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(4);
	private static readonly TimeSpan CollectionWindow = TimeSpan.FromSeconds(8);

	private readonly Network _network;
	private readonly Uri? _currentCoordinator;
	private CancellationTokenSource? _refreshCts;

	[AutoNotify] private DiscoveredCoordinatorItem? _selectedCoordinator;
	[AutoNotify] private string? _statusMessage;

	public DiscoverCoordinatorsDialogViewModel(UiContext context, Network network, string currentCoordinatorUri)
	{
		UiContext = context;
		_network = network;
		Uri.TryCreate(currentCoordinatorUri, UriKind.Absolute, out _currentCoordinator);

		Coordinators = new ObservableCollection<DiscoveredCoordinatorItem>();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = true;

		var canUseSelected = this.WhenAnyValue(x => x.SelectedCoordinator).Select(x => x is not null);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, SelectedCoordinator?.Uri), canUseSelected);

		var canRefresh = this.WhenAnyValue(x => x.IsBusy).Select(busy => !busy);
		RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
	}

	public ObservableCollection<DiscoveredCoordinatorItem> Coordinators { get; }

	public ICommand RefreshCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		disposables.Add(Disposable.Create(() =>
		{
			_refreshCts?.Cancel();
			_refreshCts?.Dispose();
			_refreshCts = null;
		}));

		_ = RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		_refreshCts?.Cancel();
		_refreshCts?.Dispose();
		_refreshCts = new CancellationTokenSource();
		var token = _refreshCts.Token;

		if (Services.Config?.UseTor is TorMode.Disabled)
		{
			StatusMessage = "Coordinator discovery requires Tor. Enable Tor in Connections settings.";
			return;
		}

		IsBusy = true;
		StatusMessage = "Fetching coordinators from Nostr relays...";

		try
		{
			var relayUris = Constants.DefaultNostrRelayUris.Select(x => new Uri(x)).ToArray();
			using var client = NostrClientFactory.Create(relayUris, Services.TorSettings.SocksEndpoint);

			var discovered = await CoordinatorDiscoveryClient
				.FetchAsync(client, _network, CollectionWindow, token)
				.ConfigureAwait(true);

			var registry = CoordinatorRegistry.CreateOrLoadFromFile(Services.DataDir);
			var now = DateTimeOffset.UtcNow;
			registry.Register(discovered.Select(c => c.PubKey), now);

			var cutoff = now - FreshnessWindow;
			var items = discovered
				.Where(c => c.CreatedAt >= cutoff)
				.OrderBy(c => registry.GetFirstSeen(c.PubKey))
				.Select(c => new DiscoveredCoordinatorItem(c, registry.GetFirstSeen(c.PubKey), now, IsCurrent(c.CoordinatorUri)))
				.ToList();

			Coordinators.Clear();
			foreach (var item in items)
			{
				Coordinators.Add(item);
			}

			SelectedCoordinator = null;
			StatusMessage = items.Count == 0
				? "No active coordinators found. Try again in a moment."
				: null;
		}
		catch (OperationCanceledException)
		{
			StatusMessage = null;
		}
		catch (Exception ex)
		{
			Logger.LogError($"Coordinator discovery failed: {ex}");
			StatusMessage = "Failed to fetch coordinators. See logs for details.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool IsCurrent(Uri coordinatorUri) =>
		_currentCoordinator is not null &&
		Uri.Compare(_currentCoordinator, coordinatorUri, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
}

public class DiscoveredCoordinatorItem
{
	public DiscoveredCoordinatorItem(DiscoveredCoordinator coordinator, DateTimeOffset firstSeen, DateTimeOffset now, bool isCurrentlyConnected)
	{
		Name = coordinator.Name;
		Uri = coordinator.CoordinatorUri;
		Description = coordinator.Description;
		HasDescription = !string.IsNullOrWhiteSpace(coordinator.Description);
		IsCurrentlyConnected = isCurrentlyConnected;
		FirstSeenDescription = DescribeAge(now - firstSeen);
	}

	public string Name { get; }
	public Uri Uri { get; }
	public string UriString => Uri.ToString();
	public string Description { get; }
	public bool HasDescription { get; }
	public bool IsCurrentlyConnected { get; }
	public string FirstSeenDescription { get; }

	private static string DescribeAge(TimeSpan age)
	{
		if (age < TimeSpan.FromMinutes(1))
		{
			return "new";
		}
		if (age < TimeSpan.FromDays(1))
		{
			return "first seen today";
		}
		var days = (int)Math.Round(age.TotalDays);
		return days == 1 ? "first seen yesterday" : $"first seen {days} days ago";
	}
}
