using System;
using System.Collections.Generic;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public class NavigationStack : ReactiveObject, INavigationManager
	{
		private readonly Stack<INavigatable> _backStack;
		private INavigatable _currentPage;
		private INavigatable _previousPage;
		private bool _canNavigateBack;

		public NavigationStack()
		{
			_backStack = new Stack<INavigatable>();
		}

		public INavigatable CurrentPage
		{
			get => _currentPage;
			set => this.RaiseAndSetIfChanged(ref _currentPage, value);
		}

		public INavigatable PreviousPage
		{
			get => _previousPage;
			set => this.RaiseAndSetIfChanged(ref _previousPage, value);
		}

		public bool CanNavigateBack
		{
			get => _canNavigateBack;
			set => this.RaiseAndSetIfChanged(ref _canNavigateBack, value);
		}

		public void Navigate(INavigatable viewmodel, NavigationMode mode = NavigationMode.Normal)
		{
			switch (mode)
			{
				case NavigationMode.Normal:
					if (CurrentPage is { })
					{
						_backStack.Push(CurrentPage);

						CurrentPage = viewmodel;
					}
					break;

				case NavigationMode.Clear:
					break;

				case NavigationMode.Skip:
					break;

				case NavigationMode.Swap:
					break;
			}

			UpdateCanNavigateBack();
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