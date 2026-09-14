using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.LogicalTree;

namespace WalletWasabi.Fluent.Mobile.Controls;

public sealed class MobilePage : HeaderedContentControl
{
	public static readonly StyledProperty<ICommand?> BackCommandProperty = AvaloniaProperty.Register<MobilePage, ICommand?>(nameof(BackCommand));
	public static readonly StyledProperty<bool> ShowBackProperty = AvaloniaProperty.Register<MobilePage, bool>(nameof(ShowBack));
	public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<MobilePage, object?>(nameof(Footer));
	public static readonly StyledProperty<object?> HeaderActionsProperty = AvaloniaProperty.Register<MobilePage, object?>(nameof(HeaderActions));
	public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<MobilePage, bool>(nameof(IsBusy));
	public ICommand? BackCommand { get => GetValue(BackCommandProperty); set => SetValue(BackCommandProperty, value); }
	public bool ShowBack { get => GetValue(ShowBackProperty); set => SetValue(ShowBackProperty, value); }
	public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
	public object? HeaderActions { get => GetValue(HeaderActionsProperty); set => SetValue(HeaderActionsProperty, value); }
	public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property != FooterProperty && change.Property != HeaderActionsProperty) return;
		// Extra content slots participate in inheritance even before a presenter is realized.
		if (change.OldValue is ILogical previous) LogicalChildren.Remove(previous);
		if (change.NewValue is ILogical next) LogicalChildren.Add(next);
	}
}

public sealed class MobileActionButton : Button
{
	public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<MobileActionButton, string>(nameof(Icon), "shield");
	public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<MobileActionButton, string>(nameof(Label), "");
	public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<MobileActionButton, bool>(nameof(IsSelected));
	public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
	public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
	public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsSelectedProperty) PseudoClasses.Set(":selected", IsSelected);
		if (change.Property == LabelProperty) Avalonia.Automation.AutomationProperties.SetName(this, Label);
	}
}

public sealed class MobileSectionConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is not null && parameter is not null && string.Equals(
			System.Convert.ToString(value, CultureInfo.InvariantCulture),
			System.Convert.ToString(parameter, CultureInfo.InvariantCulture),
			StringComparison.Ordinal);
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Avalonia.Data.BindingOperations.DoNothing;
}
