using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Moq;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public static class Mocks
{
	public static UiContext ContextStub()
	{
		return new UiContext(Mock.Of<IQrCodeGenerator>(x => x.Generate(It.IsAny<string>()) == Observable.Return(new bool[0, 0])), Mock.Of<IQrCodeReader>(), Mock.Of<IClipboard>(), new NullWalletList());
	}

	public static UiContext ContextWith(INavigationStack<RoutableViewModel> navigationStack)
	{
		var uiContext = new UiContext(Mock.Of<IQrCodeGenerator>(x => x.Generate(It.IsAny<string>()) == Observable.Return(new bool[0, 0])), Mock.Of<IQrCodeReader>(), Mock.Of<IClipboard>(), new NullWalletList());
		uiContext.RegisterNavigation(new TestNavigation(navigationStack));
		return uiContext;
	}

	private class TestNavigation : INavigate
	{
		private readonly INavigationStack<RoutableViewModel> _ns;

		public TestNavigation(INavigationStack<RoutableViewModel> ns)
		{
			_ns = ns;
		}

		public INavigationStack<RoutableViewModel> HomeScreen => throw new NotSupportedException();
		public INavigationStack<RoutableViewModel> DialogScreen => throw new NotSupportedException();
		public INavigationStack<RoutableViewModel> FullScreen => throw new NotSupportedException();
		public INavigationStack<RoutableViewModel> CompactDialogScreen => throw new NotSupportedException();
		public IObservable<bool> IsDialogOpen => throw new NotSupportedException();

		public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
		{
			return _ns;
		}

		public FluentNavigate To()
		{
			throw new NotSupportedException();
		}

		public Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target = NavigationTarget.Default, NavigationMode navigationMode = NavigationMode.Normal)
		{
			return _ns.NavigateDialogAsync(dialog, navigationMode);
		}
	}
}
