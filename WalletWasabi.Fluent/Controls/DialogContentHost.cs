using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Manages and hosts dialogs when it's bound to <see cref="IDialogHost"/> objects.
	/// </summary>
	public class DialogContentHost : TemplatedControl
	{
		public static readonly StyledProperty<object> ContentProperty =
			AvaloniaProperty.Register<DialogContentHost, object>(nameof(Content));

		public static readonly StyledProperty<bool> IsDialogOpenProperty =
			AvaloniaProperty.Register<DialogContentHost, bool>(nameof(IsDialogOpen));

		static DialogContentHost()
		{
			IsDialogOpenProperty.Changed.AddClassHandler<DialogContentHost>(UpdatePseudoClasses);
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

		private static void UpdatePseudoClasses(DialogContentHost arg1, AvaloniaPropertyChangedEventArgs arg2)
		{
			arg1.PseudoClasses.Set(":open", (bool)arg2.NewValue);
		}
	}
}
