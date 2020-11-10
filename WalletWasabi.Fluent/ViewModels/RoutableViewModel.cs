using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class RoutableViewModel : ViewModelBase, IRoutableViewModel
	{
		protected RoutableViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget)
		{
			NavigationState = navigationState;

			NigationTarget = navigationTarget;

			BackCommand = ReactiveCommand.Create(() => GoBack());

			CancelCommand = ReactiveCommand.Create(() => ClearNavigation());
		}

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen => NigationTarget switch
		{
			NavigationTarget.DialogScreen => NavigationState.DialogScreen.Invoke(),
			_ => NavigationState.HomeScreen.Invoke(),
		};

		public NavigationStateViewModel NavigationState { get; }

		public NavigationTarget NigationTarget { get; }

		public void Navigate()
		{
			switch (NigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					NavigationState.HomeScreen.Invoke().Router.Navigate.Execute(this);
					break;

				case NavigationTarget.DialogScreen:
					NavigationState.DialogScreen.Invoke().Router.Navigate.Execute(this);
					break;
			}
		}

		public void NavigateAndReset()
		{
			switch (NigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					NavigationState.HomeScreen.Invoke().Router.NavigateAndReset.Execute(this);
					break;

				case NavigationTarget.DialogScreen:
					NavigationState.DialogScreen.Invoke().Router.NavigateAndReset.Execute(this);
					break;
			}
		}

		public ICommand BackCommand { get; protected set; }

		public ICommand CancelCommand { get; protected set; }

		public void GoBack()
		{
			switch (NigationTarget)
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

		public void ClearNavigation()
		{
			switch (NigationTarget)
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
	}
}