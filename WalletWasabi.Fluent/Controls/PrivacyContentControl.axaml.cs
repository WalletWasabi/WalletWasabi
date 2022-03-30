using Avalonia;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Controls;

public enum ReplacementMode
{
	Text,
	Icon
}

public class PrivacyContentControl : ContentControl
{
	private CompositeDisposable? _disposable;

	public static readonly StyledProperty<bool> IsPrivacyContentVisibleProperty =
		AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(IsPrivacyContentVisible));

	public static readonly StyledProperty<bool> IsContentVisibleProperty =
		AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(IsContentVisible));

	public static readonly StyledProperty<uint> NumberOfPrivacyCharsProperty =
		AvaloniaProperty.Register<PrivacyContentControl, uint>(nameof(NumberOfPrivacyChars), 5);

	public static readonly StyledProperty<string> PrivacyTextProperty =
		AvaloniaProperty.Register<PrivacyContentControl, string>(nameof(PrivacyText));

	public static readonly StyledProperty<ReplacementMode> PrivacyReplacementModeProperty =
		AvaloniaProperty.Register<PrivacyContentControl, ReplacementMode>(nameof(PrivacyReplacementMode));

	public static readonly StyledProperty<bool> ForceShowProperty =
		AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(ForceShow));

	private bool IsPrivacyContentVisible
	{
		get => GetValue(IsPrivacyContentVisibleProperty);
		set => SetValue(IsPrivacyContentVisibleProperty, value);
	}

	private bool IsContentVisible
	{
		get => GetValue(IsContentVisibleProperty);
		set => SetValue(IsContentVisibleProperty, value);
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

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (Design.IsDesignMode)
		{
			return;
		}

		base.OnAttachedToVisualTree(e);

		_disposable = new CompositeDisposable();

		Services.UiConfig
			.WhenAnyValue(x => x.PrivacyMode)
			.Merge(this.WhenAnyValue(x => x.ForceShow))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				var value = !Services.UiConfig.PrivacyMode || ForceShow;

				IsPrivacyContentVisible = !value;
				IsContentVisible = value;
			})
			.DisposeWith(_disposable);

		PrivacyText = TextHelpers.GetPrivacyMask((int)NumberOfPrivacyChars);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);

		_disposable?.Dispose();
		_disposable = null;
	}
}
