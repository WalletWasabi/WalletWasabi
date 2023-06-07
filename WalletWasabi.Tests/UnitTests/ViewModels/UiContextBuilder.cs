using Avalonia.Input.Platform;
using Moq;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class UiContextBuilder
{
	public INavigate Navigate { get; private set; } = Mock.Of<INavigate>();
	public IQrCodeGenerator QrGenerator { get; } = Mock.Of<IQrCodeGenerator>();
	public IQrCodeReader QrReader { get; } = Mock.Of<IQrCodeReader>();
	public IClipboard Clipboard { get; private set; } = Mock.Of<IClipboard>();
	public IWalletRepository WalletRepository { get; private set; } = new NullWalletRepository();

	public UiContextBuilder WithDialogThatReturns(object value)
	{
		Navigate = new NavigationMock((value, DialogResultKind.Normal));
		return this;
	}

	public UiContextBuilder WithClipboard(IClipboard clipboard)
	{
		Clipboard = clipboard;
		return this;
	}

	public UiContext Build()
	{
		var uiContext = new UiContext(QrGenerator, QrReader, Clipboard, WalletRepository);
		uiContext.RegisterNavigation(Navigate);
		return uiContext;
	}
}
