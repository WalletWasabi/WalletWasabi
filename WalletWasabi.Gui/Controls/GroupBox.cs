using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Gui.Controls
{
	public class GroupBox : ContentControl
	{
		public static readonly StyledProperty<object> TitleProperty =
			AvaloniaProperty.Register<GroupBox, object>(nameof(Title));

		public object Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}
	}
}
