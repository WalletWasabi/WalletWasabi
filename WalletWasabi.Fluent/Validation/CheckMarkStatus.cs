using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace WalletWasabi.Fluent.Validation
{
	public class CheckMarkStatus
	{
		public static readonly AttachedProperty<bool> IsEnabledProperty =
			AvaloniaProperty.RegisterAttached<CheckMarkStatus, IAvaloniaObject, bool>("IsEnabled");

		public static bool GetIsEnabled(IAvaloniaObject obj)
		{
			return obj.GetValue(IsEnabledProperty);
		}

		public static void SetIsEnabled(IAvaloniaObject obj, bool value)
		{
			obj.SetValue(IsEnabledProperty, value);
		}
	}
}