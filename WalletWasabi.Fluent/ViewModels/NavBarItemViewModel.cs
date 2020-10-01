using ReactiveUI;
using System;
using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		public HomePageViewModel(IScreen screen) : base(screen)
		{
			Title = "Home";
		}
	}

	public class SettingsPageViewModel : NavBarItemViewModel
	{
		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() =>
			{
				screen.Router.Navigate.Execute(new HomePageViewModel(screen));
			});
		}

		public ICommand NextCommand { get; }
	}

	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		public AddWalletPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Add Wallet";
		}
	}

	public abstract class NavBarItemViewModel : ViewModelBase, IRoutableViewModel
	{
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		public NavBarItemViewModel(IScreen screen)
		{
			HostScreen = screen;
		}

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen { get; }

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
		}
	}
}
