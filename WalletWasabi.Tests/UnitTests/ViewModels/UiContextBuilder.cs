using System.Collections.Generic;
using System.Reactive.Linq;
using Moq;
using WalletWasabi.Announcements;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Tests.UnitTests.ViewModels.UIContext;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class UiContextBuilder
{
	public INavigate Navigate { get; private set; } = Mock.Of<INavigate>();
	public IQrCodeGenerator QrGenerator { get; } = Mock.Of<IQrCodeGenerator>();
	public IQrCodeReader QrReader { get; } = Mock.Of<IQrCodeReader>();
	public IUiClipboard Clipboard { get; private set; } = Mock.Of<IUiClipboard>();
	public IWalletRepository WalletRepository { get; private set; } = new NullWalletRepository();
	public IHardwareWalletInterface HardwareWalletInterface { get; private set; } = new NullHardwareWalletInterface();
	public IFileSystem FileSystem { get; private set; } = new NullFileSystem();
	public IClientConfig ClientConfig { get; private set; } = new NullClientConfig();
	public ITransactionBroadcasterModel TransactionBroadcaster { get; private set; } = Mock.Of<ITransactionBroadcasterModel>();

	public UiContextBuilder WithClipboard(IUiClipboard clipboard)
	{
		Clipboard = clipboard;
		return this;
	}

	public UiContext Build()
	{
		var applicationSettings = new NullApplicationSettings();
		var torStatusCheckerModel = Mock.Of<ITorStatusCheckerModel>(x => x.Issues == Observable.Empty<IList<Issue>>());
		var uiContext = new UiContext(
			QrGenerator,
			QrReader,
			Clipboard,
			WalletRepository,
			Mock.Of<CoinjoinModel>(),
			HardwareWalletInterface,
			FileSystem,
			ClientConfig,
			applicationSettings,
			TransactionBroadcaster,
			Mock.Of<AmountProvider>(),
			new EditableSearchSource(),
			torStatusCheckerModel,
			new HealthMonitor(applicationSettings, torStatusCheckerModel),
			Mock.Of<ReleaseHighlights>(),
			 null!);

		uiContext.RegisterNavigation(Navigate);
		return uiContext;
	}
}
