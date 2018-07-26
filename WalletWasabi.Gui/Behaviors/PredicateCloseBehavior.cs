using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Behaviors
{
	public class PredicateCloseBehavior : Behavior<Window>
	{
		private CompositeDisposable _disposables;

		/// <summary>
		/// Define <see cref="CanClose"/> property.
		/// </summary>
		public static readonly AvaloniaProperty<bool> CanCloseProperty =
			AvaloniaProperty.Register<PredicateCloseBehavior, bool>(nameof(CanClose));

		public bool CanClose
		{
			get => GetValue(CanCloseProperty);
			set => SetValue(CanCloseProperty, value);
		}

		private static bool _closing = false;
		protected override void OnAttached()
		{
			base.OnAttached();

			_disposables = new CompositeDisposable
			{
				Observable.FromEventPattern<CancelEventArgs>(AssociatedObject, nameof(AssociatedObject.Closing)).Subscribe(ev =>
				{
					if(_closing) return;
					if(!CanClose)
					{
						ev.EventArgs.Cancel = true;

						MainWindowViewModel.Instance.ShowDialogAsync(new CannotCloseDialogViewModel()).GetAwaiter();
					}
					_closing = true;
				})
			};
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposables.Dispose();
		}
	}
}
