using System.Windows.Input;
using ReactiveUI;
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

			OpenCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					owner.IsBusy = true;
					var view = await NavigationManager.MaterialiseViewModel(metaData);

					if (view is { })
					{
						Navigate(view.DefaultTarget).To(view);
					}

					owner.IsBusy = false;
				});
		}

		public new string Title => _metaData.Title;

		public string Caption => _metaData.Caption;

		public int Order => _metaData.Order;

		public SearchCategory Category { get; }

		public string[] Keywords => _metaData.Keywords;

		public string[] XamlKeywords => _metaData.XamlKeywords;

		public ICommand OpenCommand { get; }

		public override string IconName => _metaData.IconName;
	}
}