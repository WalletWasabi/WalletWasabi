using System;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.NavBar
{
	public enum NavBarItemSelectionMode
	{
		Selected = 0,
		Button = 1,
		Toggle = 2
	}

	public abstract class NavBarItemViewModel : RoutableViewModel
	{
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		protected NavBarItemViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, NavBarItemSelectionMode mode) : base(navigationState, navigationTarget)
		{
			_title = "";
			Mode = mode;
			OpenCommand = ReactiveCommand.Create(NavigateToSelf);
		}

		public NavBarItemViewModel? Parent { get; set; }

		public abstract string IconName { get; }

		public NavBarItemSelectionMode Mode { get; }

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
			set
			{
				switch ( Mode)
				{
					case NavBarItemSelectionMode.Selected:
						this.RaiseAndSetIfChanged(ref _isSelected, value);
						break;
					case NavBarItemSelectionMode.Button:
					case NavBarItemSelectionMode.Toggle:
						break;
				}
			}
		}

		public ICommand OpenCommand { get; protected set; }

		public virtual void Toggle()
		{
		}
	}
}