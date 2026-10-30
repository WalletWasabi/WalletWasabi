using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls;

public enum ReplacementMode
{
	Text,
	Icon,
	None
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
	private readonly ReplaySubject<bool> _isContentRevealed = new(1);

	public PrivacyContentControl()
	{
		IsContentRevealed = _isContentRevealed.AsObservable();
	}

	public IObservable<bool> IsContentRevealed { get; }

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (Design.IsDesignMode)
		{
			return;
		}

		var privacyModeObservable = PrivacyModeHelper.GetPrivacyModeEnabled(this) ?? Observable.Return(false);

		PrivacyModeHelper.DelayedRevealAndHide(
				this.WhenAnyValue(x => x.IsPointerOver),
				privacyModeObservable,
				this.WhenAnyValue(x => x.ForceShow))
			.Subscribe(_isContentRevealed)
			.DisposeWith(_disposables);
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
