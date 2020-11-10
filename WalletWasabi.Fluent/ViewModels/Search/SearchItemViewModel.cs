using System;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemViewModel : RoutableViewModel
	{
		public SearchItemViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, string iconName, string title, Func<RoutableViewModel> createTargetView) : base(navigationState, navigationTarget)
		{
			IconName = iconName;

			Title = title;

			OpenCommand = ReactiveCommand.Create(() =>
			{
				NavigateToTargetView(navigationState, navigationTarget, createTargetView);
			});
		}

		public string IconName { get; }

		public string Title { get; }

		public ICommand OpenCommand { get; }

		private void NavigateToTargetView(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, Func<RoutableViewModel> createTargetView)
		{
			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.Home:
					navigationState.HomeScreen?.Invoke().Router.Navigate.Execute(createTargetView());
					break;

				case NavigationTarget.Dialog:
					navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(createTargetView());
					break;
			}
		}

		public override string ToString()
		{
			return Title;
		}
	}
}