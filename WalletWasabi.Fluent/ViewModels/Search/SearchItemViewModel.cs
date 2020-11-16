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
			SearchCategory category,
			string keywords,
			Func<RoutableViewModel> createTargetView) : base(navigationState, navigationTarget)
		{
			IconName = iconName;

			Category = category;

			Keywords = keywords;

			Title = title;

			OpenCommand = ReactiveCommand.Create(() => NavigateTo(createTargetView(), navigationTarget));
		}

		public string IconName { get; }

		public string Title { get; }

		public SearchCategory Category { get; }

		public string Keywords { get; }

		public ICommand OpenCommand { get; }

		public override string ToString()
		{
			return Keywords;
		}
	}
}