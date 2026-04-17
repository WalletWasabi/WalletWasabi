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
public partial class DiscoverCoordinatorsDialogViewModel : DialogViewModelBase<CoordinatorConnectionString?>
{
	private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(4);
	private static readonly TimeSpan CollectionWindow = TimeSpan.FromSeconds(8);
	private const int MaxResults = 10;

	private readonly Network _network;
	private readonly string _currentCoordinatorUri;
	private CancellationTokenSource? _refreshCts;

	[AutoNotify] private DiscoveredCoordinatorItem? _selectedCoordinator;
	[AutoNotify] private string? _statusMessage;

	public DiscoverCoordinatorsDialogViewModel(UiContext context, Network network, string currentCoordinatorUri)
	{
		UiContext = context;
		_network = network;
		_currentCoordinatorUri = currentCoordinatorUri;

		Coordinators = new ObservableCollection<DiscoveredCoordinatorItem>();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = true;

		var canUseSelected = this.WhenAnyValue(x => x.SelectedCoordinator).Select(x => x is not null);
		NextCommand = ReactiveCommand.Create(OnUseSelected, canUseSelected);

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

			var cutoff = DateTimeOffset.UtcNow - FreshnessWindow;

			var items = discovered
				.Where(c => c.CreatedAt >= cutoff)
				.Where(c => !IsPlaceholderUri(c.CoordinatorUri))
				.OrderByDescending(c => c.AbsoluteMinInputCount)
				.ThenByDescending(c => c.CreatedAt)
				.Take(MaxResults)
				.Select(c => new DiscoveredCoordinatorItem(c, IsCurrent(c.CoordinatorUri)))
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

	private static bool IsPlaceholderUri(Uri coordinatorUri)
	{
		var host = coordinatorUri.Host;
		return string.Equals(host, "api.example.com", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(host, "example.com", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
	}

	private bool IsCurrent(Uri coordinatorUri)
	{
		if (string.IsNullOrWhiteSpace(_currentCoordinatorUri))
		{
			return false;
		}

		if (!Uri.TryCreate(_currentCoordinatorUri, UriKind.Absolute, out var currentUri))
		{
			return false;
		}

		return Uri.Compare(currentUri, coordinatorUri, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
	}

	private void OnUseSelected()
	{
		if (SelectedCoordinator is not { } selected)
		{
			return;
		}

		var coordinator = selected.Coordinator;
		var connection = new CoordinatorConnectionString(
			coordinator.Name,
			coordinator.Network,
			coordinator.CoordinatorUri,
			coordinator.AbsoluteMinInputCount,
			coordinator.ReadMoreUri ?? coordinator.CoordinatorUri);

		Close(DialogResultKind.Normal, connection);
	}
}

public class DiscoveredCoordinatorItem
{
	public DiscoveredCoordinatorItem(DiscoveredCoordinator coordinator, bool isCurrentlyConnected)
	{
		Coordinator = coordinator;
		IsCurrentlyConnected = isCurrentlyConnected;
	}

	public DiscoveredCoordinator Coordinator { get; }
	public bool IsCurrentlyConnected { get; }
	public string Name => Coordinator.Name;
	public string CoordinatorUriString => Coordinator.CoordinatorUri.ToString();
	public int AbsoluteMinInputCount => Coordinator.AbsoluteMinInputCount;
	public bool HasMinInputCount => Coordinator.AbsoluteMinInputCount > 0;
	public string Description => string.IsNullOrWhiteSpace(Coordinator.Description) ? Coordinator.Name : Coordinator.Description;
	public bool HasDescription => !string.IsNullOrWhiteSpace(Coordinator.Description);
}
