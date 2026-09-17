using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>A stacked settings field that retains the input control's native validation and focus behavior.</summary>
public sealed class MobileSettingsField : HeaderedContentControl
{
	public static readonly StyledProperty<string?> DescriptionProperty =
		AvaloniaProperty.Register<MobileSettingsField, string?>(nameof(Description));
	public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}
