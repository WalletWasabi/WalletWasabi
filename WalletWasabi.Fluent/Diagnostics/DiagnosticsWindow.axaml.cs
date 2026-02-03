using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Diagnostics;

public partial class DiagnosticsWindow : Window
{
	private readonly Control? _control;
	private Style? _pointerOverStyle;
	private Style? _isVisibleFalseStyle;
	private Style? _focusWithinStyle;
	private Style? _focusStyle;
	private Style? _focusVisibleStyle;
	private Style? _disabledStyle;

	public DiagnosticsWindow()
	{
		InitializeComponent();
/*#if DEBUG
		this.AttachDevTools();
#endif*/
	}

	public DiagnosticsWindow(Control control)
	{
		InitializeComponent();

		_control = control;

		DefaultDiagnostics();

/*#if DEBUG
		this.AttachDevTools();
#endif*/
	}

	public TopLevel? Root { get; set; }

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	protected override void OnClosing(WindowClosingEventArgs e)
	{
		base.OnClosing(e);

		RemoveDiagnostics();
	}

	private void DefaultDiagnostics()
	{
		if (_control is { })
		{
			ToggleStyle(true, "PointerOverDiagnosticStyle", _control, ref _pointerOverStyle);
			ToggleStyle(true, "FocusWithinDiagnosticStyle", _control, ref _focusWithinStyle);
			ToggleStyle(true, "FocusDiagnosticStyle", _control, ref _focusStyle);
		}
	}

	private void RemoveDiagnostics()
	{
		if (_control is { })
		{
			if (_pointerOverStyle is { })
			{
				_control.Styles.Remove(_pointerOverStyle);
				_pointerOverStyle = null;
			}

			if (_isVisibleFalseStyle is { })
			{
				_control.Styles.Remove(_isVisibleFalseStyle);
				_isVisibleFalseStyle = null;
			}

			if (_focusWithinStyle is { })
			{
				_control.Styles.Remove(_focusWithinStyle);
				_focusWithinStyle = null;
			}

			if (_focusStyle is { })
			{
				_control.Styles.Remove(_focusStyle);
				_focusStyle = null;
			}

			if (_focusVisibleStyle is { })
			{
				_control.Styles.Remove(_focusVisibleStyle);
				_focusVisibleStyle = null;
			}

			if (_disabledStyle is { })
			{
				_control.Styles.Remove(_disabledStyle);
				_disabledStyle = null;
			}

			_control.InvalidateVisual();
		}
	}

	private void ToggleStyle(bool isEnabled, string key, Control control, ref Style? style)
	{
		if (isEnabled)
		{
			style = Resources[key] as Style;
			if (style is { })
			{
				control.Styles.Add(style);
				control.InvalidateVisual();
			}
		}
		else
		{
			if (style is { })
			{
				control.Styles.Remove(style);
				control.InvalidateVisual();
				style = null;
			}
		}
	}

	private void PointerOverCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _control is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			ToggleStyle(isEnabled, "PointerOverDiagnosticStyle", _control, ref _pointerOverStyle);
		}
	}

	private void FocusWithinCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _control is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			ToggleStyle(isEnabled, "FocusWithinDiagnosticStyle", _control, ref _focusWithinStyle);
		}
	}

	private void FocusCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _control is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			ToggleStyle(isEnabled, "FocusDiagnosticStyle", _control, ref _focusStyle);
		}
	}

	private void FocusVisibleCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _control is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			ToggleStyle(isEnabled, "FocusVisibleDiagnosticStyle", _control, ref _focusVisibleStyle);
		}
	}

	private void IsVisibleFalseCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _control is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			ToggleStyle(isEnabled, "IsVisibleFalseDiagnosticStyle", _control, ref _isVisibleFalseStyle);
		}
	}

	private void DisabledCheckBox_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is CheckBox checkBox && _control is { })
		{
			var isEnabled = checkBox.IsChecked == true;
			ToggleStyle(isEnabled, "DisabledDiagnosticStyle", _control, ref _disabledStyle);
		}
	}
}
