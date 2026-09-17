using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>A two-state setting with a native toggle automation/keyboard contract.</summary>
public sealed class MobilePreferenceToggle : ToggleButton
{
	public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<MobilePreferenceToggle, string>(nameof(Label), "");
	public static readonly StyledProperty<string> DescriptionProperty = AvaloniaProperty.Register<MobilePreferenceToggle, string>(nameof(Description), "");
	public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
	public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == LabelProperty) AutomationProperties.SetName(this, Label);
		if (change.Property == DescriptionProperty) AutomationProperties.SetHelpText(this, Description);
	}
}

/// <summary>Exposes only the appearance modes supported by persisted wallet settings.</summary>
public sealed class MobileAppearanceSelector : TemplatedControl
{
	public static readonly StyledProperty<bool> IsDarkProperty = AvaloniaProperty.Register<MobileAppearanceSelector, bool>(nameof(IsDark), defaultBindingMode: BindingMode.TwoWay);
	private Button? _light;
	private Button? _dark;
	public MobileAppearanceSelector() => PseudoClasses.Set(":light", true);
	public bool IsDark { get => GetValue(IsDarkProperty); set => SetValue(IsDarkProperty, value); }
	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		if (_light is not null) _light.Click -= ChooseLight;
		if (_dark is not null) _dark.Click -= ChooseDark;
		base.OnApplyTemplate(e);
		_light = e.NameScope.Find<Button>("PART_Light");
		_dark = e.NameScope.Find<Button>("PART_Dark");
		if (_light is not null) _light.Click += ChooseLight;
		if (_dark is not null) _dark.Click += ChooseDark;
	}
	private void ChooseLight(object? sender, RoutedEventArgs e) => SetCurrentValue(IsDarkProperty, false);
	private void ChooseDark(object? sender, RoutedEventArgs e) => SetCurrentValue(IsDarkProperty, true);
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property != IsDarkProperty) return;
		PseudoClasses.Set(":dark", IsDark);
		PseudoClasses.Set(":light", !IsDark);
	}
}
