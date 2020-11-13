using System;
using System.Threading.Tasks;
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
						if (resetNavigation)
						{
							NavigationState.HomeScreen.Invoke().Router.NavigateAndReset.Execute(this);
						}
						else
						{
							NavigationState.HomeScreen.Invoke().Router.Navigate.Execute(this);
						}
					}
					break;

				case NavigationTarget.DialogScreen:
					{
						if (resetNavigation)
						{
							NavigationState.DialogScreen.Invoke().Router.NavigateAndReset.Execute(this);
						}
						else
						{
							NavigationState.DialogScreen.Invoke().Router.Navigate.Execute(this);
						}
					}
					break;

				case NavigationTarget.DialogHost:
					if (viewModel is DialogViewModelBase dialog && NavigationState.DialogHost?.Invoke() is IDialogHost dialogHost)
					{
						dialogHost.CurrentDialog = dialog;
					}
					break;

				default:
					break;
			}
		}

		public void NavigateToSelf() => NavigateTo(this, NavigationTarget, false);

		public void NavigateToSelfAndReset() => NavigateTo(this, NavigationTarget, true);

		public void GoBack(NavigationTarget navigationTarget)
		{
			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					NavigationState.HomeScreen.Invoke().Router.NavigateBack.Execute();
					break;

				case NavigationTarget.DialogScreen:
					NavigationState.DialogScreen.Invoke().Router.NavigateBack.Execute();
					break;
			}
		}

		public void GoBack() => GoBack(NavigationTarget);

		public void ClearNavigation(NavigationTarget navigationTarget)
		{
			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					NavigationState.HomeScreen.Invoke().Router.NavigationStack.Clear();
					break;

				case NavigationTarget.DialogScreen:
					NavigationState.DialogScreen.Invoke().Router.NavigationStack.Clear();
					break;
			}
		}

		public void ClearNavigation() => ClearNavigation(NavigationTarget);
	}
}