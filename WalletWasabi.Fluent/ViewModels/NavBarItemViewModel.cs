using ReactiveUI;
using System;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class NavBarItemViewModel : ViewModelBase, IRoutableViewModel
	{
		private NavigationStateViewModel _navigationState;
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		public NavBarItemViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget)
		{
			_navigationState = navigationState;

			OpenCommand = ReactiveCommand.Create(() =>
			{
				switch (navigationTarget)
				{
					case NavigationTarget.Default:
					case NavigationTarget.Home:
						_navigationState.HomeScreen().Router.Navigate.Execute(this);
						break;
					case NavigationTarget.Dialog:
						_navigationState.DialogScreen().Router.Navigate.Execute(this);
						break;
				}
			});
		}

		public NavBarItemViewModel Parent { get; set; }

		public abstract string IconName { get; }

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen => _navigationState.HomeScreen();

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
	}
}
