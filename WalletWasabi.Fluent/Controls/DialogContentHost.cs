using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Manages and hosts dialogs when it's bound to <see cref="IContent"/> objects.
	/// </summary>
	public class DialogContentHost : TemplatedControl
	{
		public static readonly StyledProperty<DialogViewModelBase> ContentProperty =
			AvaloniaProperty.Register<DialogContentHost, DialogViewModelBase>(nameof(Content));

		public static readonly StyledProperty<ICommand> CloseDialogCommandProperty =
			AvaloniaProperty.Register<DialogContentHost, ICommand>(nameof(CloseDialogCommand));

		static DialogContentHost()
		{
			ContentProperty.Changed.AddClassHandler<DialogContentHost>(OnContentPropertyChanged);
		}

		public DialogContentHost()
		{
			CloseDialogCommand = ReactiveCommand.Create(() => Content?.CloseDialog());
		}

		/// <summary>
		/// Gets or sets the VM that implements <see cref="IContent"/>
		/// </summary>
		public DialogViewModelBase Content
		{
			get => GetValue(ContentProperty);
			set => SetValue(ContentProperty, value);
		}

		public ICommand CloseDialogCommand
		{
			get => GetValue(CloseDialogCommandProperty);
			set => SetValue(CloseDialogCommandProperty, value);
		}

		private static void OnContentPropertyChanged(DialogContentHost arg1, AvaloniaPropertyChangedEventArgs arg2)
		{
			(arg2.NewValue as DialogViewModelBase)?.WhenAnyValue(x => x.IsDialogOpen).Subscribe(x =>
			{
				arg1.PseudoClasses.Set(":open", x);
			});
		}
	}
}
