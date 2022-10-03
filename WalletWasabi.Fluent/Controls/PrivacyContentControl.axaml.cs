using System.Reactive.Linq;
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
	public static readonly StyledProperty<uint> NumberOfPrivacyCharsProperty =
		AvaloniaProperty.Register<PrivacyContentControl, uint>(nameof(NumberOfPrivacyChars), 5);

	public static readonly StyledProperty<ReplacementMode> PrivacyReplacementModeProperty =
		AvaloniaProperty.Register<PrivacyContentControl, ReplacementMode>(nameof(PrivacyReplacementMode));

	public static readonly StyledProperty<bool> ForceShowProperty =
		AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(ForceShow));

	public PrivacyContentControl()
	{
		if (Design.IsDesignMode)
		{
			return;
		}

		var displayContent = PrivacyModeHelper.DelayedRevealAndHide(
			this.WhenAnyValue(x => x.IsPointerOver),
			Services.UiConfig.WhenAnyValue(x => x.PrivacyMode),
			this.WhenAnyValue(x => x.ForceShow));

		IsContentRevealed = displayContent
			.ReplayLastActive();

		PrivacyText = this.WhenAnyValue(x => x.NumberOfPrivacyChars)
			.Select(n => TextHelpers.GetPrivacyMask((int) n))
			.ReplayLastActive();
	}

	private IObservable<string> PrivacyText { get; } = Observable.Empty<string>();

	private IObservable<bool> IsContentRevealed { get; } = Observable.Empty<bool>();

	public uint NumberOfPrivacyChars
	{
		get => GetValue(NumberOfPrivacyCharsProperty);
		set => SetValue(NumberOfPrivacyCharsProperty, value);
	}

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
}
