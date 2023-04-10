using System.Reactive.Linq;
using Avalonia.Input.Platform;
using Moq;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public static class Mocks
{
	public static UiContext ContextWith(INavigate navigation)
	{
		var uiContext = GetUiContext();
		uiContext.RegisterNavigation(navigation);
		return uiContext;
	}

	private static UiContext GetUiContext()
	{
		return new UiContext(Mock.Of<IQrCodeGenerator>(x => x.Generate(It.IsAny<string>()) == Observable.Return(new bool[0, 0])), Mock.Of<IClipboard>());
	}
}
