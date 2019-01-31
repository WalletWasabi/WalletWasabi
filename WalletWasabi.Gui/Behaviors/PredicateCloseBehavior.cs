using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
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

		public Task DialogWindowTask = null;

		public bool CanClose
		{
			get => GetValue(CanCloseProperty);
			set => SetValue(CanCloseProperty, value);
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			_disposables = new CompositeDisposable
			{
				Observable.FromEventPattern<CancelEventArgs>(AssociatedObject, nameof(AssociatedObject.Closing)).Subscribe(async ev =>
				{
					if(!CanClose)
					{
						ev.EventArgs.Cancel = true;
					}
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
