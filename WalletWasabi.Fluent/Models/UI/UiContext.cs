using System.Threading.Tasks;
using DynamicData;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;

	public UiContext(IQrCodeGenerator qrCodeGenerator, IQrCodeReader qrCodeReader, IUiClipboard clipboard, IWalletRepository walletRepository, IHardwareWalletInterface hardwareWalletInterface, IFileSystem fileSystem, IClientConfig config, IApplicationSettings applicationSettings)
	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		QrCodeReader = qrCodeReader ?? throw new ArgumentNullException(nameof(qrCodeReader));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		WalletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
		HardwareWalletInterface = hardwareWalletInterface ?? throw new ArgumentNullException(nameof(hardwareWalletInterface));
		FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		Config = config ?? throw new ArgumentNullException(nameof(config));
		ApplicationSettings = applicationSettings ?? throw new ArgumentNullException(nameof(applicationSettings));
		CustomSearch = new CustomSearch();
	}

	public IUiClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }
	public IWalletRepository WalletRepository { get; }
	public IQrCodeReader QrCodeReader { get; }
	public IHardwareWalletInterface HardwareWalletInterface { get; }
	public IFileSystem FileSystem { get; }
	public IClientConfig Config { get; }
	public IApplicationSettings ApplicationSettings { get; }
	public ICustomSearch CustomSearch { get; set; }
	
	/// <summary>
	/// The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be testable)
	/// </summary>
	public static UiContext Default;

	public void RegisterNavigation(INavigate navigate)
	{
		_navigate ??= navigate;
	}

	public INavigate Navigate()
	{
		return _navigate ?? throw new InvalidOperationException($"{GetType().Name} {nameof(_navigate)} hasn't been initialized.");
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return
			_navigate?.Navigate(target)
			?? throw new InvalidOperationException($"{GetType().Name} {nameof(_navigate)} hasn't been initialized.");
	}
}

public interface ICustomSearch : ISearchSource
{
	void Remove(ComposedKey key);
	void Add(ISearchItem searchItem);
}

public class CustomSearch : ICustomSearch
{
	private readonly SourceCache<ISearchItem, ComposedKey> _actions;

	public CustomSearch()
	{
		_actions = new SourceCache<ISearchItem, ComposedKey>(x => x.Key);
		Changes = _actions.Connect();
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }
	public void Remove(ComposedKey key)
	{
		_actions.RemoveKey(key);
	}

	public void Add(ISearchItem searchItem)
	{
		_actions.AddOrUpdate(searchItem);
	}
}
