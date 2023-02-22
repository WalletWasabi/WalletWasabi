using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class PreviewItem : ContentControl
{
	public static readonly StyledProperty<string> LabelProperty =
		AvaloniaProperty.Register<PreviewItem, string>(nameof(Label));

	public static readonly StyledProperty<Geometry> IconProperty =
		AvaloniaProperty.Register<PreviewItem, Geometry>(nameof(Icon));

	public static readonly StyledProperty<double> IconSizeProperty =
		AvaloniaProperty.Register<PreviewItem, double>(nameof(IconSize), 24);

	public static readonly StyledProperty<bool> IsIconVisibleProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(IsIconVisible));

	public static readonly StyledProperty<string> TextValueProperty =
		AvaloniaProperty.Register<PreviewItem, string>(nameof(TextValue));

	public static readonly StyledProperty<ICommand> CopyCommandProperty =
		AvaloniaProperty.Register<PreviewItem, ICommand>(nameof(CopyCommand));

	public static readonly StyledProperty<bool> IsCopyButtonVisibleProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(IsCopyButtonVisible));

	public static readonly StyledProperty<bool> PrivacyModeEnabledProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(PrivacyModeEnabled));

	public string Label
	{
		get => GetValue(LabelProperty);
		set => SetValue(LabelProperty, value);
	}

	public Geometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public double IconSize
	{
		get => GetValue(IconSizeProperty);
		set => SetValue(IconSizeProperty, value);
	}

	public bool IsIconVisible
	{
		get => GetValue(IsIconVisibleProperty);
		set => SetValue(IsIconVisibleProperty, value);
	}

	public string TextValue
	{
		get => GetValue(TextValueProperty);
		set => SetValue(TextValueProperty, value);
	}

	public ICommand CopyCommand
	{
		get => GetValue(CopyCommandProperty);
		set => SetValue(CopyCommandProperty, value);
	}

	public bool IsCopyButtonVisible
	{
		get => GetValue(IsCopyButtonVisibleProperty);
		set => SetValue(IsCopyButtonVisibleProperty, value);
	}

	public bool PrivacyModeEnabled
	{
		get => GetValue(PrivacyModeEnabledProperty);
		set => SetValue(PrivacyModeEnabledProperty, value);
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		var button = e.NameScope.Find<ClipboardCopyButton>("PART_ClipboardCopyButton");

		var isCopyButtonVisible =
			button.CopyCommand.IsExecuting
			.CombineLatest(this.WhenAnyValue(x => x.IsPointerOver, x => x.TextValue, (a, b) => a && !string.IsNullOrWhiteSpace(b)))
			.Select(x => x.First || x.Second);

		this.Bind(IsCopyButtonVisibleProperty, isCopyButtonVisible);

		base.OnApplyTemplate(e);
	}
}
