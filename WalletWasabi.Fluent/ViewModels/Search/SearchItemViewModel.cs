using System;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemViewModel : RoutableViewModel
	{
		public SearchItemViewModel(
			NavigationStateViewModel navigationState,
			NavigationTarget navigationTarget,
			string iconName,
			string title,
			string category,
			string keywords,
			Func<RoutableViewModel> createTargetView) : base(navigationState, navigationTarget)
		{
			IconName = iconName;

			Category = category;

			Keywords = keywords;

			Title = title;

			OpenCommand = ReactiveCommand.Create(
				() => NavigateToTargetView(navigationState, navigationTarget, createTargetView));
		}

		public string IconName { get; }

		public string Title { get; }

		public string Category { get; }

		public string Keywords { get; }

		public ICommand OpenCommand { get; }

		private void NavigateToTargetView(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, Func<RoutableViewModel> createTargetView)
		{
			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					navigationState.HomeScreen?.Invoke().Router.Navigate.Execute(createTargetView());
					break;

				case NavigationTarget.DialogScreen:
					navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(createTargetView());
					break;
			}
		}

		public override string ToString()
		{
			return Keywords;
		}
	}
}