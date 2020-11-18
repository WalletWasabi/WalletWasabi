using System;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemViewModel : RoutableViewModel
	{
		public SearchItemViewModel(
			string title,
			string caption,
			int order,
			SearchCategory category,
			string keywords,
			string iconName,
			NavigationStateViewModel navigationState,
			NavigationTarget navigationTarget,
			Func<RoutableViewModel> createTargetView) : base(navigationState, navigationTarget)
		{
			IconName = iconName;
			Title = title;
			Caption = caption;
			Order = order;
			Category = category;
			Keywords = keywords;
			OpenCommand = ReactiveCommand.Create(() => NavigateTo(createTargetView(), navigationTarget));
		}

		public string IconName { get; }

		public string Title { get; }

		public string Caption { get; }

		public int Order { get; }

		public SearchCategory Category { get; }

		public string Keywords { get; }

		public ICommand OpenCommand { get; }
	}
}