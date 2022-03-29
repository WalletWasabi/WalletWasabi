using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
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

	public static readonly StyledProperty<string> ValueProperty =
		AvaloniaProperty.Register<PreviewItem, string>(nameof(Value));

	public static readonly StyledProperty<ICommand> CopyCommandProperty =
		AvaloniaProperty.Register<PreviewItem, ICommand>(nameof(CopyCommand));

	public static readonly StyledProperty<bool> CopyButtonVisibilityProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(CopyButtonVisibility));

	public static readonly StyledProperty<bool> PrivacyModeEnabledProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(PrivacyModeEnabled));

	public PreviewItem()
	{
		this.Bind(CopyButtonVisibilityProperty, this.WhenAnyValue(item => item.IsPointerOver));
	}

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

	public string Value
	{
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public ICommand CopyCommand
	{
		get => GetValue(CopyCommandProperty);
		set => SetValue(CopyCommandProperty, value);
	}

	public bool CopyButtonVisibility
	{
		get => GetValue(CopyButtonVisibilityProperty);
		set => SetValue(CopyButtonVisibilityProperty, value);
	}

	public bool PrivacyModeEnabled
	{
		get => GetValue(PrivacyModeEnabledProperty);
		set => SetValue(PrivacyModeEnabledProperty, value);
	}
}
