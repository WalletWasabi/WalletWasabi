using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Manages and hosts dialogs when it's bound to <see cref="IDialogHost"/> objects.
	/// </summary>
	public class DialogContentHost : TemplatedControl
	{
		public static readonly StyledProperty<IDialogHost> DialogHostProperty =
			AvaloniaProperty.Register<DialogContentHost, IDialogHost>(nameof(DialogHost));

		static DialogContentHost()
		{
			DialogHostProperty.Changed.AddClassHandler<DialogContentHost>(OnDialogHostPropertyChanged);
		}

		private static void OnDialogHostPropertyChanged(DialogContentHost arg1, AvaloniaPropertyChangedEventArgs arg2)
		{
			(arg2.NewValue as IDialogHost)?.SetDialogStateListener(x => 
			{
				arg1.PseudoClasses.Set(":open", x);
			});
		}
 
		/// <summary>
		/// Gets or sets the VM that implements <see cref="IDialogHost"/>
		/// </summary>
		public IDialogHost DialogHost
		{
			get => GetValue(DialogHostProperty);
			set => SetValue(DialogHostProperty, value);
		}
	}
}
