using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>A wrapping, headered detail field with an optional existing Wasabi clipboard action.</summary>
public sealed class MobileDetailRow : HeaderedContentControl
{
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<MobileDetailRow, string?>(nameof(CopyText));

    public string? CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }
}
