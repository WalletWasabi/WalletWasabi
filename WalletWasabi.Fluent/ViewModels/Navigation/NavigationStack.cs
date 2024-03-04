using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public partial class NavigationStack<T> : ViewModelBase, INavigationStack<T> where T : class, INavigatable
{
	private readonly Stack<T> _backStack;
	[AutoNotify] private T? _currentPage;
	[AutoNotify] private bool _canNavigateBack;
	private bool _operationsEnabled = true;

	protected NavigationStack()
	{
		_backStack = new Stack<T>();
	}

	protected IEnumerable<T> Stack => _backStack;

	protected virtual void OnNavigated(T? oldPage, bool oldInStack, T? newPage, bool newInStack)
	{
	}

	protected virtual void OnPopped(T page)
	{
	}

	private void NavigationOperation(T? oldPage, bool oldInStack, T? newPage, bool newInStack)
	{
		if (_operationsEnabled)
		{
			oldPage?.OnNavigatedFrom(oldInStack);
		}

		CurrentPage = newPage;

		if (!oldInStack && oldPage is { })
		{
			OnPopped(oldPage);
		}

		if (_operationsEnabled)
		{
			OnNavigated(oldPage, oldInStack, newPage, newInStack);
		}

		if (_operationsEnabled && newPage is { })
		{
			newPage.OnNavigatedTo(newInStack);
		}

		UpdateCanNavigateBack();
	}

	public virtual void Clear()
	{
		Clear(false);
	}

	protected virtual void Clear(bool keepRoot)
	{
		var root = _backStack.Count > 0 ? _backStack.Last() : CurrentPage;

		if ((keepRoot && CurrentPage == root) || (!keepRoot && _backStack.Count == 0 && CurrentPage is null))
		{
			return;
		}

		var oldPage = CurrentPage;

		var oldItems = _backStack.ToList();

		_backStack.Clear();

		if (keepRoot)
		{
			foreach (var item in oldItems)
			{
				if (item != root)
				{
					OnPopped(item);
				}
			}
			CurrentPage = root;
		}
		else
		{
			foreach (var item in oldItems)
			{
				if (item is INavigatable navigatable)
				{
					navigatable.OnNavigatedFrom(false);
				}

				OnPopped(item);
			}

			CurrentPage = null;
		}

		NavigationOperation(oldPage, false, CurrentPage, CurrentPage is { });
	}

	public void BackTo(T viewModel)
	{
		if (CurrentPage == viewModel)
		{
			return;
		}

		if (_backStack.Contains(viewModel))
		{
			var oldPage = CurrentPage;

			while (_backStack.Pop() != viewModel)
			{
			}

			NavigationOperation(oldPage, false, viewModel, true);
		}
	}

	public void BackTo<TViewModel>() where TViewModel : T
	{
		var previous = _backStack.Reverse().SingleOrDefault(x => x is TViewModel);

		if (previous is { })
		{
			BackTo(previous);
		}
	}

	public void To(T viewModel, NavigationMode mode = NavigationMode.Normal)
	{
		var oldPage = CurrentPage;

		bool oldInStack = true;
		bool newInStack = false;

		switch (mode)
		{
			case NavigationMode.Normal:
				if (oldPage is { })
				{
					_backStack.Push(oldPage);
				}
				break;

			case NavigationMode.Clear:
				oldInStack = false;
				_operationsEnabled = false;
				Clear();
				_operationsEnabled = true;
				break;

			case NavigationMode.Skip:
				// Do not push old page on the back stack.
				break;
		}

		NavigationOperation(oldPage, oldInStack, viewModel, newInStack);
	}

	public void Back()
	{
		if (_backStack.Count > 0)
		{
			var oldPage = CurrentPage;

			CurrentPage = _backStack.Pop();

			NavigationOperation(oldPage, false, CurrentPage, true);
		}
		else
		{
			Clear(); // in this case only CurrentPage might be set and Clear will provide correct behavior.
		}
	}

	public FluentNavigate To()
	{
		return new FluentNavigate(UiContext);
	}

	private void UpdateCanNavigateBack()
	{
		CanNavigateBack = _backStack.Count > 0;
	}

	public async Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialogTask = dialog.GetDialogResultAsync();

		var t = dialog as T;

		if (CurrentPage != t)
		{
			To(t, navigationMode);
		}

		var result = await dialogTask;

		Back();

		return result;
	}
}
