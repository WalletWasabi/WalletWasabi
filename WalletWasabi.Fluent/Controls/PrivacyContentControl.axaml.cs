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
	private static readonly TimeSpan RevealDelay = TimeSpan.FromSeconds(0.75);

	public static readonly StyledProperty<uint> NumberOfPrivacyCharsProperty =
		AvaloniaProperty.Register<PrivacyContentControl, uint>(nameof(NumberOfPrivacyChars), 5);

	public static readonly StyledProperty<string> PrivacyTextProperty =
		AvaloniaProperty.Register<PrivacyContentControl, string>(nameof(PrivacyText));

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

		var isPrivacyModeEnabled = Services.UiConfig.WhenAnyValue(x => x.PrivacyMode);
		var isPointerOver = this.WhenAnyValue(x => x.IsPointerOver).DelayTrue(RevealDelay);
		var isForced = this.WhenAnyValue(x => x.ForceShow);

		var displayContent = isPrivacyModeEnabled.CombineLatest(
			isPointerOver,
			isForced,
			(privacyModeEnabled, pointerOver, forced) => !privacyModeEnabled || pointerOver || forced);

		IsContentRevealed = displayContent
			.ObserveOn(RxApp.MainThreadScheduler)
			.Replay(1).RefCount();

		PrivacyText = TextHelpers.GetPrivacyMask((int) NumberOfPrivacyChars);
	}
	
	public uint NumberOfPrivacyChars
	{
		get => GetValue(NumberOfPrivacyCharsProperty);
		set => SetValue(NumberOfPrivacyCharsProperty, value);
	}

	private string PrivacyText
	{
		get => GetValue(PrivacyTextProperty);
		set => SetValue(PrivacyTextProperty, value);
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

	private IObservable<bool> IsContentRevealed { get; } = Observable.Empty<bool>();
}
