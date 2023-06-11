using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Onboarding.Pages;
public partial class FirstView : UserControl
{
	public FirstView()
	{
		InitializeComponent();
	}


	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
