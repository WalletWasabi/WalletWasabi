using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;
public class UnitControl : TemplatedControl
{
	public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<UnitControl, string>("Value");

	public string Value
	{
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public static readonly StyledProperty<string> UnitProperty = AvaloniaProperty.Register<UnitControl, string>("Unit");

	public string Unit
	{
		get => GetValue(UnitProperty);
		set => SetValue(UnitProperty, value);
	}
}
