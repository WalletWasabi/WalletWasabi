using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Gui.Behaviors
{
	public sealed class FadeOutBehavior : Behavior<Control>
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private DispatcherTimer _timer;
		private Animation _fadeOutAnimation;
		private Border _border;

/*
        public static readonly AvaloniaProperty IsVisibleProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsVisible", typeof(FadeOutBehavior), true, true, BindingMode.TwoWay);

        public static bool GetIsVisible(Control control)
        {
            return (bool)control.GetValue(IsVisibleProperty);
        }
        public static void SetIsVisible(Control control, bool value)
        {
            control.SetValue(IsVisibleProperty, value);
        }
*/


		/// <inheritdoc/>
		protected override void OnAttached()
		{
			base.OnAttached();
			AssociatedObject.AttachedToLogicalTree += (s, e)=>{
				_border = AssociatedObject.FindControl<Border>("Frame");
			};

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(5)
			};

			_timer.Tick += async (sender, e) =>
			{
				_timer.Stop();
				var fadeOutAnimation = new Animation
				{
					Children =
					{
						new KeyFrame()
						{
							Setters =
							{
								new Setter
								{
									Property = Visual.OpacityProperty,
									Value = 0d
								}
							},
							Cue = new Cue(1d)
						}
					},
					Duration = TimeSpan.FromSeconds(0.5),
					//Delay = TimeSpan.FromSeconds(7)
				};

				await fadeOutAnimation.RunAsync(AssociatedObject);
				AssociatedObject.IsVisible = false;
			};


			var visibilitySubscription = AssociatedObject.GetObservable(Control.IsVisibleProperty).Subscribe(visible =>
			{
				if (visible)
				{
					_timer.Start();
				}
			});

			var pointerEnterSubscription = AssociatedObject.GetObservable(Control.PointerEnterEvent).Subscribe(pointer =>
			{
				AssociatedObject.IsVisible = true;
				_border.BorderThickness = new Thickness(1);
				_border.BorderBrush = Brush.Parse("#888888");
				_timer.Stop();
			});
			var pointerLeaveSubscription = AssociatedObject.GetObservable(Control.PointerLeaveEvent).Subscribe(pointer =>
			{
				_border.BorderThickness = new Thickness(0);
				_timer.Start();
			});

			Disposables.Add(pointerEnterSubscription);
			Disposables.Add(pointerLeaveSubscription);
			Disposables.Add(visibilitySubscription);
		}

		/// <inheritdoc/>
		protected override void OnDetaching()
		{
			base.OnDetaching();
			Disposables.Dispose();
		}
	}
}
