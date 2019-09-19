using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	public class GreyOutOnDisabledBehavior : Behavior<Control>
	{
		private CompositeDisposable Disposables { get; set; }

		private double? OriginalOpacity { get; set; }

		protected override void OnAttached()
		{
			Disposables?.Dispose();

			Disposables = new CompositeDisposable
			{
				AssociatedObject
					.GetObservable(InputElement.IsEnabledProperty)
					.Subscribe(enabled =>
					{
						if (enabled)
						{
							AssociatedObject.Opacity = OriginalOpacity ?? 1;
						}
						else
						{
							OriginalOpacity = AssociatedObject.Opacity;
							AssociatedObject.Opacity = 0.5;
						}
					})
			};
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
			Disposables = null;
		}
	}
}
