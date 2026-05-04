using WalletWasabi.Announcements;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;

	public UiContext(
		IServices services,
		QrCodeGenerator qrCodeGenerator,
		QrCodeReader qrCodeReader,
		UiClipboard clipboard,
		WalletRepository walletRepository,
		CoinjoinModel coinJoinModel,
		HardwareWalletInterface hardwareWalletInterface,
		FileSystemModel fileSystem,
		ClientConfigModel config,
		ApplicationSettings applicationSettings,
		TransactionBroadcasterModel transactionBroadcaster,
		AmountProvider amountProvider,
		EditableSearchSource editableSearchSource,
		TorStatusCheckerModel torStatusChecker,
		HealthMonitor healthMonitor,
		ReleaseHighlights releaseHighlights,
		Client.Scheme? scheme = null)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		QrCodeReader = qrCodeReader ?? throw new ArgumentNullException(nameof(qrCodeReader));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		WalletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
		CoinjoinModel = coinJoinModel ?? throw new ArgumentNullException(nameof(coinJoinModel));
		HardwareWalletInterface = hardwareWalletInterface ?? throw new ArgumentNullException(nameof(hardwareWalletInterface));
		FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		Config = config ?? throw new ArgumentNullException(nameof(config));
		ApplicationSettings = applicationSettings ?? throw new ArgumentNullException(nameof(applicationSettings));
		TransactionBroadcaster = transactionBroadcaster ?? throw new ArgumentNullException(nameof(transactionBroadcaster));
		AmountProvider = amountProvider ?? throw new ArgumentNullException(nameof(amountProvider));
		EditableSearchSource = editableSearchSource ?? throw new ArgumentNullException(nameof(editableSearchSource));
		TorStatusChecker = torStatusChecker ?? throw new ArgumentNullException(nameof(torStatusChecker));
		HealthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
		ReleaseHighlights = releaseHighlights ?? throw new ArgumentNullException(nameof(releaseHighlights));
		Scheme = scheme;
	}

	public IServices Services { get; }
	public UiClipboard Clipboard { get; }
	public QrCodeGenerator QrCodeGenerator { get; }
	public WalletRepository WalletRepository { get; }
	public CoinjoinModel CoinjoinModel { get; }
	public QrCodeReader QrCodeReader { get; }
	public HardwareWalletInterface HardwareWalletInterface { get; }
	public FileSystemModel FileSystem { get; }
	public ClientConfigModel Config { get; }
	public ApplicationSettings ApplicationSettings { get; }
	public TransactionBroadcasterModel TransactionBroadcaster { get; }
	public AmountProvider AmountProvider { get; }
	public EditableSearchSource EditableSearchSource { get; }
	public TorStatusCheckerModel TorStatusChecker { get; }
	public HealthMonitor HealthMonitor { get; }
	public ReleaseHighlights ReleaseHighlights { get; }
	public Client.Scheme? Scheme { get; }
	public MainViewModel? MainViewModel { get; private set; }

	public void RegisterNavigation(INavigate navigate)
	{
		_navigate ??= navigate;
	}

	public INavigate Navigate()
	{
		return _navigate ?? throw new InvalidOperationException($"{GetType().Name} {nameof(Navigate)} hasn't been initialized.");
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return
			_navigate?.Navigate(target)
			?? throw new InvalidOperationException($"{GetType().Name} {nameof(Navigate)} hasn't been initialized.");
	}

	public void SetMainViewModel(MainViewModel viewModel)
	{
		MainViewModel ??= viewModel;
	}
}
