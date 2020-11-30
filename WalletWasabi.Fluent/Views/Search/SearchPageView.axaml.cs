using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Search;

namespace WalletWasabi.Fluent.Views.Search
{
	public class SearchPageView : ReactiveUserControl<SearchPageViewModel>
	{
		public SearchPageView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}