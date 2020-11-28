using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.NavBar
{
	public abstract class NavBarItemViewModel : RoutableViewModel
	{
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		protected NavBarItemViewModel(NavigationStateViewModel navigationState) : base(navigationState)
		{
			_title = "";
			OpenCommand = ReactiveCommand.Create(NavigateToSelf);
		}

		public NavBarItemViewModel? Parent { get; set; }

		public abstract string IconName { get; }

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

		public ICommand OpenCommand { get; protected set; }
	}
}