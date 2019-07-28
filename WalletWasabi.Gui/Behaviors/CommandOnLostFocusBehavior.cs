using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace WalletWasabi.Gui.Behaviors
{
	internal class CommandOnLostFocusBehavior : CommandBasedBehavior<Control>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Disposables.Add(AssociatedObject.AddHandler(InputElement.LostFocusEvent, (sender, e) =>
			{
				e.Handled = ExecuteCommand();
			}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
