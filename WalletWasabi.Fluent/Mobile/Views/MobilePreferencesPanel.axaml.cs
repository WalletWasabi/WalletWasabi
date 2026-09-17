using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Bindable UI only; the host supplies and persists the actual application preferences.</summary>
public sealed class MobilePreferencesPanel : UserControl
{
	public static readonly StyledProperty<bool> IsDarkProperty = AvaloniaProperty.Register<MobilePreferencesPanel, bool>(nameof(IsDark), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<bool> PrivacyModeProperty = AvaloniaProperty.Register<MobilePreferencesPanel, bool>(nameof(PrivacyMode), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<bool> AutoCopyProperty = AvaloniaProperty.Register<MobilePreferencesPanel, bool>(nameof(AutoCopy), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<bool> AutoPasteProperty = AvaloniaProperty.Register<MobilePreferencesPanel, bool>(nameof(AutoPaste), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<MobilePreferencesPanel, bool>(nameof(IsReadOnly), true);
	public MobilePreferencesPanel()
	{
		AvaloniaXamlLoader.Load(this);
		this.FindControl<StackPanel>("PreferencesRoot")!.DataContext = this;
	}
	public bool IsDark { get => GetValue(IsDarkProperty); set => SetValue(IsDarkProperty, value); }
	public bool PrivacyMode { get => GetValue(PrivacyModeProperty); set => SetValue(PrivacyModeProperty, value); }
	public bool AutoCopy { get => GetValue(AutoCopyProperty); set => SetValue(AutoCopyProperty, value); }
	public bool AutoPaste { get => GetValue(AutoPasteProperty); set => SetValue(AutoPasteProperty, value); }
	public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
}
