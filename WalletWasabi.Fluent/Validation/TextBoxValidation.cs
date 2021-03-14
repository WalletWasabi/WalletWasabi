using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace WalletWasabi.Fluent.Validation
{
	public class TextBoxValidation : AvaloniaObject
	{
		public static readonly AttachedProperty<bool> IsEnabledProperty =
			AvaloniaProperty.RegisterAttached<IAvaloniaObject, bool>("IsEnabled", typeof(TextBoxValidation),
				defaultBindingMode: BindingMode.TwoWay);

		public static bool GetIsEnabled(IAvaloniaObject obj)
		{
			return (bool) obj.GetValue(IsEnabledProperty);
		}

		public static void SetIsEnabled(IAvaloniaObject obj, bool value)
		{
			obj.SetValue(IsEnabledProperty, value);
		}
	}
}