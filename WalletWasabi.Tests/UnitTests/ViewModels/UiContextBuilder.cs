using Avalonia.Input.Platform;
using Moq;
using WalletWasabi.Fluent.Models.UI;
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
		var uiContext = new UiContext(QrGenerator, QrReader, Clipboard);
		uiContext.RegisterNavigation(Navigate);
		return uiContext;
	}
}
