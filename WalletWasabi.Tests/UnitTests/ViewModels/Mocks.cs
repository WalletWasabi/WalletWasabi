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
	public static UIContext ContextWithDialogResult<T>(T desired)
	{
		var uiContext = GetUIContext();
		uiContext.RegisterNavigation(new DialogResultNavigation<T>(desired));
		return uiContext;
	}

	public static UIContext ContextWith(INavigationStack<RoutableViewModel> navigationStack)
	{
		return ContextWith(new TestNavigation(navigationStack));
	}

	public static UIContext ContextWith(INavigate navigation)
	{
		var uiContext = GetUIContext();
		uiContext.RegisterNavigation(navigation);
		return uiContext;
	}

	private static UIContext GetUIContext()
	{
		return new UIContext(Mock.Of<IQrCodeGenerator>(x => x.Generate(It.IsAny<string>()) == Observable.Return(new bool[0, 0])), Mock.Of<IClipboard>());
	}

	private class TestNavigation : INavigate
	{
		private readonly INavigationStack<RoutableViewModel> _navigationStack;

		public TestNavigation(INavigationStack<RoutableViewModel> navigationStack)
		{
			_navigationStack = navigationStack;
		}

		public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
		{
			return _navigationStack;
		}

		public FluentNavigate To()
		{
			throw new NotImplementedException();
		}
	}
}

public class DialogResultNavigation<T> : INavigate
{
	private readonly T _desired;

	public DialogResultNavigation(T desired)
	{
		_desired = desired;
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return new DialogStack<T>(_desired);
	}

	public FluentNavigate To()
	{
		throw new NotImplementedException();
	}
}

public class DialogStack<T> : INavigationStack<RoutableViewModel>
{
	private readonly T _desired;

	public DialogStack(T desired)
	{
		_desired = desired;
	}

	public RoutableViewModel? CurrentPage { get; }
	public bool CanNavigateBack { get; }
	public void To(RoutableViewModel viewmodel, NavigationMode mode = NavigationMode.Normal)
	{
	}

	public void Back()
	{
	}

	public void BackTo(RoutableViewModel viewmodel)
	{
	}

	public void BackTo<TViewModel>() where TViewModel : RoutableViewModel
	{
	}

	public void Clear()
	{
	}
}

public class FakeDialog<T> : DialogViewModelBase<T>
{
	public FakeDialog(T desired)
	{
		Close(DialogResultKind.Normal, desired);
	}

	public override string Title { get; protected set; }
}
