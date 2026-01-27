using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
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

	private readonly CompositeDisposable _disposables = new();

	private readonly UiConfig _uiConfig = Services.UiConfig;

	public PrivacyContentControl()
	{
		if (Design.IsDesignMode)
		{
			return;
		}

		var isContentRevealed = PrivacyModeHelper.DelayedRevealAndHide(
				this.WhenAnyValue(x => x.IsPointerOver),
				this.WhenAnyValue(x => x._uiConfig.PrivacyMode),
				this.WhenAnyValue(x => x.ForceShow))
			.Replay();

		IsContentRevealed = isContentRevealed;
		isContentRevealed.Connect().DisposeWith(_disposables);
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

	protected override void OnUnloaded(RoutedEventArgs e) => _disposables.Dispose();
}
