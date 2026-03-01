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
	/// <summary>
	///     The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be
	///     testable)
	/// </summary>
	public static UiContext Default;

	private INavigate? _navigate;

	public UiContext(IQrCodeGenerator qrCodeGenerator,
		IQrCodeReader qrCodeReader,
		IUiClipboard clipboard,
		IWalletRepository walletRepository,
		CoinjoinModel coinJoinModel,
		IHardwareWalletInterface hardwareWalletInterface,
		IFileSystem fileSystem,
		IClientConfig config,
		IApplicationSettings applicationSettings,
		ITransactionBroadcasterModel transactionBroadcaster,
		AmountProvider amountProvider,
		EditableSearchSource editableSearchSource,
		ITorStatusCheckerModel torStatusChecker,
		HealthMonitor healthMonitor,
		ReleaseHighlights releaseHighlights,
		Daemon.Scheme? scheme = null)
	{
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

	public IUiClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }
	public IWalletRepository WalletRepository { get; }
	public CoinjoinModel CoinjoinModel { get; }
	public IQrCodeReader QrCodeReader { get; }
	public IHardwareWalletInterface HardwareWalletInterface { get; }
	public IFileSystem FileSystem { get; }
	public IClientConfig Config { get; }
	public IApplicationSettings ApplicationSettings { get; }
	public ITransactionBroadcasterModel TransactionBroadcaster { get; }
	public AmountProvider AmountProvider { get; }
	public EditableSearchSource EditableSearchSource { get; }
	public ITorStatusCheckerModel TorStatusChecker { get; }
	public HealthMonitor HealthMonitor { get; }
	public ReleaseHighlights ReleaseHighlights { get; }
	public Daemon.Scheme? Scheme { get; }
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
