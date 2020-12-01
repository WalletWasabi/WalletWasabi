using System;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemViewModel : NavBarItemViewModel
	{
		public SearchItemViewModel(
			string title,
			string caption,
			int order,
			SearchCategory category,
			string keywords,
			string iconName,
			Func<Task<RoutableViewModel>> createTargetView)
		{
			Title = title;
			Caption = caption;
			Order = order;
			Category = category;
			Keywords = keywords;
			IconName = iconName;
			SelectionMode = NavBarItemSelectionMode.Button;

			OpenCommand = ReactiveCommand.CreateFromTask(
				async () =>
			{
				var view = await createTargetView();

				Navigate(view.DefaultTarget).To(view);
			});
		}

		public override string IconName { get; }

		public string Caption { get; }

		public int Order { get; }

		public SearchCategory Category { get; }

		public string Keywords { get; }
	}
}