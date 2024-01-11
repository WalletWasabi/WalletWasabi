using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

	public PrivacyContentControl()
	{
		if (Design.IsDesignMode)
		{
			return;
		}

		/*var displayContent = PrivacyModeHelper.DelayedRevealAndHide(
			this.WhenAnyValue(x => x.IsPointerOver),
			Services.UiConfig.WhenAnyValue(x => x.PrivacyMode),
			this.WhenAnyValue(x => x.ForceShow));

		IsContentRevealed = displayContent
			.ReplayLastActive();*/
	}

	private IObservable<bool> IsContentRevealed { get; } = Observable.Empty<bool>();

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
