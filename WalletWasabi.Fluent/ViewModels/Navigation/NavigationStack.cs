using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public class TargettedNavigationStack : NavigationStack<RoutableViewModel>
	{
		private readonly NavigationTarget _target;

		public TargettedNavigationStack(NavigationTarget target)
		{
			_target = target;
		}

		public override void Clear()
		{
			if (_target == NavigationTarget.HomeScreen)
			{
				base.Clear(true);
			}
			else
			{
				base.Clear();
			}
		}

		protected override void OnPopped(RoutableViewModel page)
		{
			base.OnPopped(page);

			page.CurrentTarget = NavigationTarget.Default;
		}

		protected override void OnNavigated(
			RoutableViewModel? oldPage,
			bool oldInStack,
			RoutableViewModel? newPage,
			bool newInStack)
		{
			base.OnNavigated(oldPage, oldInStack, newPage, newInStack);

			if (newPage is { })
			{
				newPage.CurrentTarget = _target;
			}
		}
	}

	public class NavigationStack<T> : ViewModelBase, INavigationManager<T> where T : class, INavigatable
	{
		private readonly Stack<T> _backStack;
		private T _currentPage;
		private T _previousPage;
		private bool _canNavigateBack;
		private bool _operationsEnabled = true;

		public NavigationStack()
		{
			_backStack = new Stack<T>();
		}

		public T? CurrentPage
		{
			get => _currentPage;
			set => this.RaiseAndSetIfChanged(ref _currentPage, value);
		}

		public bool CanNavigateBack
		{
			get => _canNavigateBack;
			set => this.RaiseAndSetIfChanged(ref _canNavigateBack, value);
		}

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

		protected IEnumerable<T> Stack => _backStack;

		public virtual void Clear()
		{
			Clear(false);
		}

		protected virtual void Clear(bool keepRoot = false)
		{
			var root = _backStack.Count > 0 ? _backStack.Last() : CurrentPage;

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
					OnPopped(item);
				}

				CurrentPage = null;
			}

			NavigationOperation(oldPage, false, CurrentPage, CurrentPage is { });
		}

		public void BackTo(T viewmodel)
		{
			if (CurrentPage == viewmodel)
			{
				return;
			}

			if (_backStack.Contains(viewmodel))
			{
				var oldPage = CurrentPage;

				while (_backStack.Pop() != viewmodel)
				{
				}

				NavigationOperation(oldPage, false, viewmodel, true);
			}
		}

		public void To(T viewmodel, NavigationMode mode = NavigationMode.Normal)
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
			}

			NavigationOperation(oldPage, oldInStack, viewmodel, newInStack);
		}

		public void Back()
		{
			if (CanNavigateBack)
			{
				var oldPage = CurrentPage;

				CurrentPage = _backStack.Pop();
			}
		}

		public void Back(Type pageType)
		{
			throw new NotImplementedException();
		}

		private void UpdateCanNavigateBack()
		{
			CanNavigateBack = _backStack.Count > 0;
		}
	}
}