using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Features.Onboarding.Pages;
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
