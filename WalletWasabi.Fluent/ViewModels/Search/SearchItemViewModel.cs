using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemViewModel : RoutableViewModel
	{
		private readonly NavigationMetaData _metaData;

		public SearchItemViewModel(
			SearchPageViewModel owner,
			NavigationMetaData metaData,
			SearchCategory category)
		{
			_metaData = metaData;
			Category = category;
			OpenCommand = ReactiveCommand.CreateFromTask(async () => await OnOpenAsync(owner, metaData));
		}

		private async Task OnOpenAsync(SearchPageViewModel owner, NavigationMetaData metaData)
		{
			owner.IsBusy = true;
			var view = await NavigationManager.MaterialiseViewModelAsync(metaData);


			if (view is NavBarItemViewModel navBarItem && navBarItem.OpenCommand.CanExecute(default))
			{
				navBarItem.OpenCommand.Execute(default);
			}
			else if (view is { })
			{
				Navigate(view.DefaultTarget).To(view);
			}

			owner.IsBusy = false;
		}

		public string Caption => _metaData.Caption;

		public int Order => _metaData.Order;

		public SearchCategory Category { get; }

		public string[] Keywords => _metaData.Keywords;

		public ICommand OpenCommand { get; }

		public override string Title
		{
			get => _metaData.Title;
			protected set { }
		}

		public override string IconName => _metaData.IconName;
	}
}
