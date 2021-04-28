using Avalonia;
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls
{
	public class AdorningContentControl : ContentControl
	{
		private IDisposable? _subscription;

		public static readonly StyledProperty<Control> AdornmentProperty =
			AvaloniaProperty.Register<ContentArea, Control>(nameof(Adornment));

		public Control Adornment
		{
			get => GetValue(AdornmentProperty);
			set => SetValue(AdornmentProperty, value);
		}

		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == IsPointerOverProperty)
			{
				Dispatcher.UIThread.Post(ShowHideRevealContent);
			}
			else if (change.Property == AdornmentProperty)
			{
				RevealContentChanged(change.OldValue.GetValueOrDefault<Control?>(), change.NewValue.GetValueOrDefault<Control?>());
			}
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);

			_subscription?.Dispose();

			RevealContentChanged(null, null);
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			RevealContentChanged(null, Adornment);
		}

		private void RevealContentChanged(Control? oldValue, Control? newValue)
		{
			_subscription?.Dispose();

			if (oldValue is { })
			{
				AdornerHelper.RemoveAdorner(this, oldValue);
			}

			if (newValue is { })
			{
				_subscription = newValue.GetObservable(IsPointerOverProperty)
					.Subscribe(_ => Dispatcher.UIThread.Post(ShowHideRevealContent));
			}
		}

		private void ShowHideRevealContent()
		{
			if (IsPointerOver)
			{
				AdornerHelper.AddAdorner(this, Adornment);
			}
			else if(!Adornment.IsPointerOver)
			{
				AdornerHelper.RemoveAdorner(this, Adornment);
			}
		}
	}
}