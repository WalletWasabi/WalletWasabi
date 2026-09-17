using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>
/// Explicitly bound privacy presentation for native pages. Hidden by default until the
/// wallet's discreet-mode preference is available; does not reveal content on hover.
/// </summary>
public sealed class MobileSensitiveContent : ContentControl
{
	public static readonly StyledProperty<bool> IsHiddenProperty =
		AvaloniaProperty.Register<MobileSensitiveContent, bool>(nameof(IsHidden), true);

	public bool IsHidden
	{
		get => GetValue(IsHiddenProperty);
		set => SetValue(IsHiddenProperty, value);
	}
}
