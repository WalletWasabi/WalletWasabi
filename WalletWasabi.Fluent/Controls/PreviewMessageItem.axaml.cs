using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class PreviewMessageItem : ContentControl
{
	public static readonly StyledProperty<string> LabelProperty =
		AvaloniaProperty.Register<PreviewMessageItem, string>(nameof(Label));

	public static readonly StyledProperty<Geometry> IconProperty =
		AvaloniaProperty.Register<PreviewMessageItem, Geometry>(nameof(Icon));

	public static readonly StyledProperty<double> IconSizeProperty =
		AvaloniaProperty.Register<PreviewMessageItem, double>(nameof(IconSize), 24);

	public static readonly StyledProperty<bool> IsIconVisibleProperty =
		AvaloniaProperty.Register<PreviewMessageItem, bool>(nameof(IsIconVisible));

	public static readonly StyledProperty<object> CopyableContentProperty =
		AvaloniaProperty.Register<PreviewMessageItem, object>(nameof(CopyableContent));

	public static readonly StyledProperty<ICommand> CopyCommandProperty =
		AvaloniaProperty.Register<PreviewMessageItem, ICommand>(nameof(CopyCommand));

	public static readonly StyledProperty<bool> IsCopyButtonVisibleProperty =
		AvaloniaProperty.Register<PreviewMessageItem, bool>(nameof(IsCopyButtonVisible));

	public static readonly StyledProperty<bool> PrivacyModeEnabledProperty =
		AvaloniaProperty.Register<PreviewMessageItem, bool>(nameof(PrivacyModeEnabled));

	public static readonly StyledProperty<Dock> CopyButtonPlacementProperty =
		AvaloniaProperty.Register<PreviewMessageItem, Dock>(nameof(CopyButtonPlacement), Dock.Right);

	public static readonly StyledProperty<ICommand?> EditCommandProperty =
		AvaloniaProperty.Register<PreviewMessageItem, ICommand?>(nameof(EditCommand));

	public static readonly StyledProperty<bool> IsEditButtonVisibleProperty =
		AvaloniaProperty.Register<PreviewMessageItem, bool>(nameof(IsEditButtonVisible));

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

	public object CopyableContent
	{
		get => GetValue(CopyableContentProperty);
		set => SetValue(CopyableContentProperty, value);
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

	public Dock CopyButtonPlacement
	{
		get => GetValue(CopyButtonPlacementProperty);
		set => SetValue(CopyButtonPlacementProperty, value);
	}

	public ICommand? EditCommand
	{
		get => GetValue(EditCommandProperty);
		set => SetValue(EditCommandProperty, value);
	}

	public bool IsEditButtonVisible
	{
		get => GetValue(IsEditButtonVisibleProperty);
		set => SetValue(IsEditButtonVisibleProperty, value);
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		var button = e.NameScope.Find<ClipboardCopyButton>("PART_ClipboardCopyButton");

		if (button is { })
		{
			var isCopyButtonVisible =
				button.CopyCommand
				      .IsExecuting
				      .CombineLatest(this.WhenAnyValue(x => x.IsPointerOver, x => x.CopyableContent, (a, b) => a && !string.IsNullOrWhiteSpace(b?.ToString())))
				      .Select(x => x.First || x.Second);

			Bind(IsCopyButtonVisibleProperty, isCopyButtonVisible);
		}

		base.OnApplyTemplate(e);
	}
}
