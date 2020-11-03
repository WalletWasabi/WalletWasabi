using System;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class RoutableViewModel : ViewModelBase, IRoutableViewModel
	{
		private NavigationStateViewModel _navigationState;
		private NavigationTarget _navigationTarget;

		protected RoutableViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget)
		{
			_navigationState = navigationState;
			_navigationTarget = navigationTarget;
		}

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen
		{
			get
			{
				return _navigationTarget switch
				{
					NavigationTarget.Dialog => _navigationState.DialogScreen?.Invoke(),
					_ => _navigationState.HomeScreen?.Invoke(),
				};
			}
		}

		public void Navigate()
		{
			switch (_navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.Home:
					_navigationState.HomeScreen?.Invoke().Router.Navigate.Execute(this);
					break;

				case NavigationTarget.Dialog:
					_navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(this);
					break;
			}
		}

		public void NavigateAndReset()
		{
			switch (_navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.Home:
					_navigationState.HomeScreen?.Invoke().Router.NavigateAndReset.Execute(this);
					break;

				case NavigationTarget.Dialog:
					_navigationState.DialogScreen?.Invoke().Router.NavigateAndReset.Execute(this);
					break;
			}
		}

		public void GoBack()
		{
			switch (_navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.Home:
					_navigationState.HomeScreen?.Invoke().Router.NavigateBack.Execute();
					break;

				case NavigationTarget.Dialog:
					_navigationState.DialogScreen?.Invoke().Router.NavigateBack.Execute();
					break;
			}
		}

		public void ClearNavigation()
		{
			switch (_navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.Home:
					_navigationState.HomeScreen?.Invoke().Router.NavigationStack.Clear();
					break;

				case NavigationTarget.Dialog:
					_navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear();
					break;
			}
		}
	}
}