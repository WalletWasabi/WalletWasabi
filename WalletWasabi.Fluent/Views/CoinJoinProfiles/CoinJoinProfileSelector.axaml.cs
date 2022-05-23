using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.CoinJoinProfiles;

public class CoinJoinProfileSelector : UserControl
{
	public static readonly StyledProperty<bool> IsSmallProperty =
		AvaloniaProperty.Register<CoinJoinProfileSelector, bool>(nameof(IsSmall));

	public CoinJoinProfileSelector()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public bool IsSmall
	{
		get => GetValue(IsSmallProperty);
		set => SetValue(IsSmallProperty, value);
	}
}
