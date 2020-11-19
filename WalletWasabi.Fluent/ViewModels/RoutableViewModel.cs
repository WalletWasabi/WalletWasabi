using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class RoutableViewModel : ViewModelBase, IRoutableViewModel
	{
		protected RoutableViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget)
		{
			NavigationState = navigationState;

			NavigationTarget = navigationTarget;

			BackCommand = ReactiveCommand.Create(() => GoBack());

			CancelCommand = ReactiveCommand.Create(() => ClearNavigation());
		}

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen => NavigationTarget switch
		{
			NavigationTarget.DialogScreen => NavigationState.DialogScreen.Invoke(),
			_ => NavigationState.HomeScreen.Invoke(),
		};

		public NavigationStateViewModel NavigationState { get; }

		public NavigationTarget NavigationTarget { get; }

		public ICommand BackCommand { get; protected set; }

		public ICommand CancelCommand { get; protected set; }

		public void NavigateTo(RoutableViewModel viewModel, NavigationTarget navigationTarget, bool resetNavigation = false)
		{
			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					{
						NavigateToHomeScreen(viewModel, resetNavigation);
					}
					break;

				case NavigationTarget.DialogScreen:
					{
						NavigateToDialogScreen(viewModel, resetNavigation);
					}
					break;

				case NavigationTarget.DialogHost:
					if (viewModel is DialogViewModelBase dialog)
					{
						NavigateToDialogHost(dialog);
					}
					break;

				default:
					break;
			}
		}

		private void NavigateToHomeScreen(RoutableViewModel viewModel, bool resetNavigation)
		{
			var command = resetNavigation ?
				NavigationState.HomeScreen?.Invoke().Router.NavigateAndReset :
				NavigationState.HomeScreen?.Invoke().Router.Navigate;
			command?.Execute(viewModel);
		}

		private void NavigateToDialogScreen(RoutableViewModel viewModel, bool resetNavigation)
		{
			var command = resetNavigation ?
				NavigationState.DialogScreen?.Invoke().Router.NavigateAndReset :
				NavigationState.DialogScreen?.Invoke().Router.Navigate;
			command?.Execute(viewModel);
		}

		private void NavigateToDialogHost(DialogViewModelBase dialog)
		{
			if (NavigationState.DialogHost?.Invoke() is IDialogHost dialogHost)
			{
				dialogHost.CurrentDialog = dialog;
			}
		}

		public void NavigateToSelf() => NavigateTo(this, NavigationTarget, false);

		public void NavigateToSelfAndReset() => NavigateTo(this, NavigationTarget, true);

		public void GoBack(NavigationTarget navigationTarget)
		{
			var router = default(RoutingState);

			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					router = NavigationState.HomeScreen.Invoke().Router;
					break;

				case NavigationTarget.DialogScreen:
					router = NavigationState.DialogScreen.Invoke().Router;
					break;
			}

			if (router is not null)
			{
				// Close all dialogs so the awaited tasks can complete.
				// - DialogViewModelBase.ShowDialogAsync()
				// - DialogViewModelBase.GetDialogResultAsync()
				if (router.CurrentViewModel is DialogViewModelBase dialog)
				{
					dialog.IsDialogOpen = false;
				}

				router.NavigateBack.Execute();
			}
		}

		public void GoBack() => GoBack(NavigationTarget);

		private void CloseDialogs(IEnumerable<IRoutableViewModel> navigationStack)
		{
			foreach (var routable in navigationStack)
			{
				// Close all dialogs so the awaited tasks can complete.
				// - DialogViewModelBase.ShowDialogAsync()
				// - DialogViewModelBase.GetDialogResultAsync()
				if (routable is DialogViewModelBase dialog)
				{
					dialog.IsDialogOpen = false;
				}
			}
		}

		public void ClearNavigation(NavigationTarget navigationTarget)
		{
			var router = default(RoutingState);

			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					router = NavigationState.HomeScreen.Invoke().Router;
					break;

				case NavigationTarget.DialogScreen:
					router = NavigationState.DialogScreen.Invoke().Router;
					break;
			}

			if (router is not null)
			{
				if (router.NavigationStack.Count >= 1)
				{
					var navigationStack = router.NavigationStack.ToList();

					router.NavigationStack.Clear();

					CloseDialogs(navigationStack);
				}
			}
		}

		public void ClearNavigation() => ClearNavigation(NavigationTarget);
	}
}