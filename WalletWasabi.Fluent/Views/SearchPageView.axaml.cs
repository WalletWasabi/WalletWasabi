using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views
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