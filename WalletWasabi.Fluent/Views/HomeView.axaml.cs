using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views
{
	public class HomeView : UserControl, IViewFor<HomeViewModel>
	{
		public HomeView()
		{
			InitializeComponent();
		}

		public HomeViewModel ViewModel { get; set; }

		object IViewFor.ViewModel { get; set; }

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
