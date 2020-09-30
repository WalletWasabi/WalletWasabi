using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views
{
	public class HomePageView : UserControl, IViewFor<HomePageViewModel>
	{
		public HomePageView()
		{
			this.InitializeComponent();
		}

		public HomePageViewModel ViewModel { get; set; }

		object IViewFor.ViewModel { get; set; }

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
