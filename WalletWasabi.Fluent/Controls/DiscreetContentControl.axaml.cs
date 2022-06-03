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

public class DiscreetContentControl : ContentControl
{
	private CompositeDisposable? _disposable;

	public static readonly StyledProperty<bool> IsDiscreetContentVisibleProperty =
		AvaloniaProperty.Register<DiscreetContentControl, bool>(nameof(IsDiscreetContentVisible));

	public static readonly StyledProperty<bool> IsContentVisibleProperty =
		AvaloniaProperty.Register<DiscreetContentControl, bool>(nameof(IsContentVisible));

	public static readonly StyledProperty<uint> NumberOfDiscreetCharsProperty =
		AvaloniaProperty.Register<DiscreetContentControl, uint>(nameof(NumberOfDiscreetChars), 5);

	public static readonly StyledProperty<string> DiscreetTextProperty =
		AvaloniaProperty.Register<DiscreetContentControl, string>(nameof(DiscreetText));

	public static readonly StyledProperty<ReplacementMode> DiscreetReplacementModeProperty =
		AvaloniaProperty.Register<DiscreetContentControl, ReplacementMode>(nameof(DiscreetReplacementMode));

	public static readonly StyledProperty<bool> ForceShowProperty =
		AvaloniaProperty.Register<DiscreetContentControl, bool>(nameof(ForceShow));

	private bool IsDiscreetContentVisible
	{
		get => GetValue(IsDiscreetContentVisibleProperty);
		set => SetValue(IsDiscreetContentVisibleProperty, value);
	}

	private bool IsContentVisible
	{
		get => GetValue(IsContentVisibleProperty);
		set => SetValue(IsContentVisibleProperty, value);
	}

	public uint NumberOfDiscreetChars
	{
		get => GetValue(NumberOfDiscreetCharsProperty);
		set => SetValue(NumberOfDiscreetCharsProperty, value);
	}

	private string DiscreetText
	{
		get => GetValue(DiscreetTextProperty);
		set => SetValue(DiscreetTextProperty, value);
	}

	public ReplacementMode DiscreetReplacementMode
	{
		get => GetValue(DiscreetReplacementModeProperty);
		set => SetValue(DiscreetReplacementModeProperty, value);
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
			.WhenAnyValue(x => x.DiscreetMode)
			.Merge(this.WhenAnyValue(x => x.ForceShow))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				var value = !Services.UiConfig.DiscreetMode || ForceShow;

				IsDiscreetContentVisible = !value;
				IsContentVisible = value;
			})
			.DisposeWith(_disposable);

		DiscreetText = TextHelpers.GetDiscreetMask((int)NumberOfDiscreetChars);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);

		_disposable?.Dispose();
		_disposable = null;
	}
}
