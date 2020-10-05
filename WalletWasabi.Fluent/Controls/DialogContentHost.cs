using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Container for NavBarItems.
	/// </summary>
	public class DialogContentHost : TemplatedControl
	{
		public static readonly StyledProperty<object> ContentProperty =
			AvaloniaProperty.Register<DialogContentHost, object>(nameof(Content));

		public static readonly StyledProperty<bool> IsDialogOpenProperty =
			AvaloniaProperty.Register<DialogContentHost, bool>(nameof(IsDialogOpen));

		public DialogContentHost()
		{
            PseudoClasses.Set(":open", IsDialogOpen);
		}

		/// <summary>
		/// The object to be displayed as a dialog.
		/// </summary>
		public object Content
		{
			get => GetValue(ContentProperty);
			set => SetValue(ContentProperty, value);
		}

		/// <summary>
		/// Gets or sets the activation state of the dialog.
		/// </summary>
		public bool IsDialogOpen
		{
			get => GetValue(IsDialogOpenProperty);
			set => SetValue(IsDialogOpenProperty, value);
		} 
	}
}
