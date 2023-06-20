using System.Threading.Tasks;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

public class NavigationMock : INavigate
{
	private readonly (object, DialogResultKind)[] _dialogResults;
	private readonly NavigationStackMock _navigationStackMock;
	private int _step;

	public NavigationMock(params (object, DialogResultKind)[] dialogResults)
	{
		_dialogResults = dialogResults;
		_navigationStackMock = new NavigationStackMock();
	}

	public int BackCount => _navigationStackMock.BackCount;

	public INavigationStack<RoutableViewModel> HomeScreen { get; }
	public INavigationStack<RoutableViewModel> DialogScreen { get; }
	public INavigationStack<RoutableViewModel> FullScreen { get; }
	public INavigationStack<RoutableViewModel> CompactDialogScreen { get; }
	public IObservable<bool> IsDialogOpen { get; }

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return _navigationStackMock;
	}

	public FluentNavigate To()
	{
		throw new NotSupportedException();
	}

	public Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target = NavigationTarget.Default, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var tuple = _dialogResults[_step];
		var result = new DialogResult<TResult>((TResult)tuple.Item1, tuple.Item2);
		return Task.FromResult(result);
	}

	private class NavigationStackMock : INavigationStack<RoutableViewModel>
	{
		public int BackCount { get; private set; }
		public RoutableViewModel? CurrentPage { get; }
		public bool CanNavigateBack { get; }

		public void To(RoutableViewModel viewmodel, NavigationMode mode = NavigationMode.Normal)
		{
			throw new NotSupportedException();
		}

		public FluentNavigate To()
		{
			throw new NotSupportedException();
		}

		public void Back()
		{
			BackCount++;
		}

		public void BackTo(RoutableViewModel viewmodel)
		{
			throw new NotSupportedException();
		}

		public void BackTo<TViewModel>() where TViewModel : RoutableViewModel
		{
			throw new NotSupportedException();
		}

		public void Clear()
		{
			throw new NotSupportedException();
		}

		public Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationMode navigationMode = NavigationMode.Normal)
		{
			throw new InvalidOperationException("Please, use the method defined in INavigate");
		}
	}
}
