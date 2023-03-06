using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace WalletWasabi.Fluent;

public partial class DiagnosticsWindow : Window
{
	public static readonly AttachedProperty<bool> EnablePointerOverDiagnosticProperty =
		AvaloniaProperty.RegisterAttached<DiagnosticsWindow, Control, bool>(
			"EnablePointerOverDiagnostic", defaultValue: false, inherits: true);

	public static readonly AttachedProperty<bool> EnableIsVisibleFalseDiagnosticProperty =
		AvaloniaProperty.RegisterAttached<DiagnosticsWindow, Control, bool>(
			"EnableIsVisibleFalseDiagnostic", defaultValue: false, inherits: true);

	public static readonly AttachedProperty<bool> EnableFocusWithinDiagnosticProperty =
		AvaloniaProperty.RegisterAttached<DiagnosticsWindow, Control, bool>(
			"EnableFocusWithinDiagnostic", defaultValue: false, inherits: true);

	public static readonly AttachedProperty<bool> EnableFocusDiagnosticProperty =
		AvaloniaProperty.RegisterAttached<DiagnosticsWindow, Control, bool>(
			"EnableFocusDiagnostic", defaultValue: false, inherits: true);

	public static readonly AttachedProperty<bool> EnableFocusVisibleDiagnosticProperty =
		AvaloniaProperty.RegisterAttached<DiagnosticsWindow, Control, bool>(
			"EnableFocusVisibleDiagnostic", defaultValue: false, inherits: true);

	public static readonly AttachedProperty<bool> EnableDisabledDiagnosticProperty =
		AvaloniaProperty.RegisterAttached<DiagnosticsWindow, Control, bool>(
			"EnableDisabledDiagnostic", defaultValue: false, inherits: true);

	private Window? _window;
	private Style? _pointerOverStyle;
	private Style? _isVisibleFalseStyle;
	private Style? _focusWithinStyle;
	private Style? _focusStyle;
	private Style? _focusVisibleStyle;
	private Style? _disabledStyle;

	public static void SetEnablePointerOverDiagnostic(Control element, bool value)
	{
		element.SetValue(EnablePointerOverDiagnosticProperty, value);
	}

	public static bool GetEnablePointerOverDiagnostic(Control element)
	{
		return element.GetValue(EnablePointerOverDiagnosticProperty);
	}

	public static void SetEnableIsVisibleFalseDiagnostic(Control element, bool value)
	{
		element.SetValue(EnableIsVisibleFalseDiagnosticProperty, value);
	}

	public static bool GetEnableIsVisibleFalseDiagnostic(Control element)
	{
		return element.GetValue(EnableIsVisibleFalseDiagnosticProperty);
	}

	public static void SetEnableFocusWithinDiagnostic(Control element, bool value)
	{
		element.SetValue(EnableFocusWithinDiagnosticProperty, value);
	}

	public static bool GetEnableFocusWithinDiagnostic(Control element)
	{
		return element.GetValue(EnableFocusWithinDiagnosticProperty);
	}

	public static void SetEnableFocusDiagnostic(Control element, bool value)
	{
		element.SetValue(EnableFocusDiagnosticProperty, value);
	}

	public static bool GetEnableFocusDiagnostic(Control element)
	{
		return element.GetValue(EnableFocusDiagnosticProperty);
	}

	public static void SetEnableFocusVisibleDiagnostic(Control element, bool value)
	{
		element.SetValue(EnableFocusVisibleDiagnosticProperty, value);
	}

	public static bool GetEnableFocusVisibleDiagnostic(Control element)
	{
		return element.GetValue(EnableFocusVisibleDiagnosticProperty);
	}

	public static void SetEnableDisabledDiagnostic(Control element, bool value)
	{
		element.SetValue(EnableDisabledDiagnosticProperty, value);
	}

	public static bool GetEnableDisabledDiagnostic(Control element)
	{
		return element.GetValue(EnableDisabledDiagnosticProperty);
	}

	public DiagnosticsWindow()
	{
		InitializeComponent();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	public DiagnosticsWindow(Window window)
	{
		InitializeComponent();

		_window = window;
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void ToggleStyle(bool isEnabled, string key, Control control, ref Style? style)
	{
		if (isEnabled)
		{
			style = Resources[key] as Style;
			if (style is { })
			{
				control.Styles.Add(style);
			}
		}
		else
		{
			if (style is { })
			{
				control.Styles.Remove(style);
			}
		}
	}

	private void PointerOverCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _window is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			SetEnablePointerOverDiagnostic(_window, isEnabled);
			ToggleStyle(isEnabled, "PointerOverDiagnosticStyle", _window, ref _pointerOverStyle);
		}
	}

	private void IsVisibleFalseCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _window is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			SetEnableIsVisibleFalseDiagnostic(_window, isEnabled);
			ToggleStyle(isEnabled, "IsVisibleFalseDiagnosticStyle", _window, ref _isVisibleFalseStyle);
		}
	}

	private void FocusWithinCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _window is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			SetEnableFocusWithinDiagnostic(_window, isEnabled);
			ToggleStyle(isEnabled, "FocusWithinDiagnosticStyle", _window, ref _focusWithinStyle);
		}
	}

	private void FocusCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _window is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			SetEnableFocusDiagnostic(_window, isEnabled);
			ToggleStyle(isEnabled, "FocusDiagnosticStyle", _window, ref _focusStyle);
		}
	}

	private void FocusVisibleCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _window is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			SetEnableFocusVisibleDiagnostic(_window, isEnabled);
			ToggleStyle(isEnabled, "FocusVisibleDiagnosticStyle", _window, ref _focusVisibleStyle);
		}
	}

	private void DisabledCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _window is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			SetEnableDisabledDiagnostic(_window, isEnabled);
			ToggleStyle(isEnabled, "DisabledDiagnosticStyle", _window, ref _disabledStyle);
		}
	}
}
