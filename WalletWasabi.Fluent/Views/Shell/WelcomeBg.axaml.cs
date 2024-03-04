using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Shell;

public partial class WelcomeBg : UserControl
{
	public static readonly StyledProperty<bool> EnableAnimationsProperty =
		AvaloniaProperty.Register<WelcomeBg, bool>(nameof(EnableAnimations));

	public WelcomeBg()
	{
		InitializeComponent();
	}

	public bool EnableAnimations
	{
		get => GetValue(EnableAnimationsProperty);
		set => SetValue(EnableAnimationsProperty, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
