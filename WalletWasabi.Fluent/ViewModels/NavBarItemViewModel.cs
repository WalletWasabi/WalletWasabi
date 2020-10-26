using ReactiveUI;
using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class NavBarItemViewModel : ViewModelBase, IRoutableViewModel
	{
		private NavigationStateViewModel _navigationState;
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		public NavBarItemViewModel(NavigationStateViewModel navigationState)
		{
			_navigationState = navigationState;
		}

		public NavBarItemViewModel Parent { get; set; }

		public abstract string IconName { get; }

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen => _navigationState.MainScreen();

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
	}
}
