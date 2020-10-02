using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views
{
	public class NavBar : UserControl
	{
		public NavBar()
		{
			this.InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
