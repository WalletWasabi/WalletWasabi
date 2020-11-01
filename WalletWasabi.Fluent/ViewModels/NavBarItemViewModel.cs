using ReactiveUI;
using System;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class NavBarItemViewModel : ViewModelBase, IRoutableViewModel
	{
		private NavigationStateViewModel _navigationState;
		private NavigationTarget _navigationTarget;
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		public NavBarItemViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget)
		{
			_navigationState = navigationState;

			_navigationTarget = navigationTarget;

			OpenCommand = ReactiveCommand.Create(() => Navigate());
		}

		public NavBarItemViewModel Parent { get; set; }

		public abstract string IconName { get; }

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen => _navigationState.HomeScreen?.Invoke();

		public bool IsExpanded
		{
			get => _isExpanded;
			set
			{
				this.RaiseAndSetIfChanged(ref _isExpanded, value);

				if (Parent != null)
				{
					Parent.IsExpanded = value;
				}
			}
		}

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}

		public ReactiveCommand<Unit, Unit> OpenCommand { get; }

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
	}
}