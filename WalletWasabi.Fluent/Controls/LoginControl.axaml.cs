using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls
{
	public class LoginControl : ContentControl
	{
		public static readonly StyledProperty<bool> IsBusyProperty =
			AvaloniaProperty.Register<ContentArea, bool>(nameof(IsBusy));

		public bool IsBusy
		{
			get => GetValue(IsBusyProperty);
			set => SetValue(IsBusyProperty, value);
		}
	}
}