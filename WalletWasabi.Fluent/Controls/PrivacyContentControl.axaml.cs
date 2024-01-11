using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls;

public enum ReplacementMode
{
	Text,
	Icon
}

public class PrivacyContentControl : ContentControl
{
	public static readonly StyledProperty<ReplacementMode> PrivacyReplacementModeProperty =
		AvaloniaProperty.Register<PrivacyContentControl, ReplacementMode>(nameof(PrivacyReplacementMode));

	public static readonly StyledProperty<bool> ForceShowProperty =
		AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(ForceShow));

	public static readonly StyledProperty<bool> UseOpacityProperty =
		AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(UseOpacity), defaultValue: true);

	public static readonly StyledProperty<int> MaxPrivacyCharsProperty =
		AvaloniaProperty.Register<PrivacyContentControl, int>(nameof(MaxPrivacyChars), int.MaxValue);

	private readonly UiConfig _uiConfig = Services.UiConfig;

	public PrivacyContentControl()
	{
		if (Design.IsDesignMode)
		{
			return;
		}

		IsContentRevealed = PrivacyModeHelper.DelayedRevealAndHide(
				this.WhenAnyValue(x => x.IsPointerOver),
				this.WhenAnyValue(x => x._uiConfig.PrivacyMode),
				this.WhenAnyValue(x => x.ForceShow))
			.ReplayLastActive();
	}

	public IObservable<bool> IsContentRevealed { get; }

	public ReplacementMode PrivacyReplacementMode
	{
		get => GetValue(PrivacyReplacementModeProperty);
		set => SetValue(PrivacyReplacementModeProperty, value);
	}

	public bool ForceShow
	{
		get => GetValue(ForceShowProperty);
		set => SetValue(ForceShowProperty, value);
	}

	public bool UseOpacity
	{
		get => GetValue(UseOpacityProperty);
		set => SetValue(UseOpacityProperty, value);
	}

	public int MaxPrivacyChars
	{
		get => GetValue(MaxPrivacyCharsProperty);
		set => SetValue(MaxPrivacyCharsProperty, value);
	}
}
