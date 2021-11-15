using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Behaviors
{
	public class NavBarSelectedIndicatorState : IDisposable
	{
		private class DiscreteEasing : Easing
		{
			public override double Ease(double progress)
			{
				return (Math.Abs(progress - 1) < double.Epsilon) ? 1 : 0;
			}
		}

		public readonly ConcurrentDictionary<int, Control> ScopeChildren = new();
		private readonly Easing bckEasing = new SplineEasing(0.2, 1, 0.1, 0.9);
		private readonly Easing fwdEasing = new SplineEasing(0.1, 0.9, 0.2);
		private readonly TimeSpan timebase = TimeSpan.FromSeconds(0.6);

		private bool _isDisposed;
		private bool _initialFixDone;
		private CancellationTokenSource _currentAnimationCts = new();

		/// <summary>
		/// The last animated indicator
		/// </summary>
		public Rectangle? PreviousIndicator { get; set; }

		// This will be used in the future for horizontal selection indicators.
		// ReSharper disable once UnusedMember.Global
		public Orientation NavItemsOrientation { get; set; } = Orientation.Vertical;

		public void Dispose()
		{
			_isDisposed = true;
			ScopeChildren.Clear();
		}

		public void AddChild(Control associatedObject)
		{
			if (ScopeChildren.ContainsKey(associatedObject.GetHashCode()))
			{
				return;
			}

			ScopeChildren.TryAdd(associatedObject.GetHashCode(),
				associatedObject);
		}

		private async void AnimateIndicators(Rectangle previousIndicator, Rectangle nextIndicator,
			CancellationToken token)
		{
			if (_isDisposed || previousIndicator is null || nextIndicator is null)
			{
				return;
			}

			// Use the prior indicator's parent as a reference point.
			var itemsContainer = previousIndicator.Parent;
			var prevVector = previousIndicator.TranslatePoint(new Point(), itemsContainer) ?? new Point();
			var nextVector = nextIndicator.TranslatePoint(new Point(), itemsContainer) ?? new Point();

			var targetVector = nextVector - prevVector;
			var fromTopToBottom = targetVector.Y > 0;
			var curEasing = fromTopToBottom ? fwdEasing : bckEasing;
			var newDim = Math.Abs(NavItemsOrientation == Orientation.Vertical ? targetVector.Y : targetVector.X);
			var maxScale = newDim / (NavItemsOrientation == Orientation.Vertical
				? nextIndicator.Bounds.Height
				: nextIndicator.Bounds.Width);

			Animation scalingAnimation = new()
			{
				Easing = curEasing,
				Duration = timebase,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, 1d)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(0.33d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, maxScale)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, 1d)
						}
					}
				}
			};

			Animation translationAnimation = new()
			{
				Easing = curEasing,
				Duration = timebase,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0d),
						Setters =
						{
							new Setter(TranslateTransform.XProperty, 0d),
							new Setter(TranslateTransform.YProperty, 0d)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(TranslateTransform.XProperty, targetVector.X),
							new Setter(TranslateTransform.YProperty, targetVector.Y)
						}
					}
				}
			};

			Animation fadeOut = new()
			{
				FillMode = FillMode.Both,
				Easing = new DiscreteEasing(),
				Duration = timebase,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0.99d),
						Setters =
						{
							new Setter(Visual.OpacityProperty, 1d)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(Visual.OpacityProperty, 0d)
						}
					}
				}
			};


			Animation fadeIn = new()
			{
				FillMode = FillMode.Both,
				Easing = new DiscreteEasing(),
				Duration = timebase,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0.99d),
						Setters =
						{
							new Setter(Visual.OpacityProperty, 0d)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(Visual.OpacityProperty, 1d)
						}
					}
				}
			};

			await Task.WhenAll(
				translationAnimation.RunAsync(previousIndicator, null, token),
				scalingAnimation.RunAsync(previousIndicator, null, token),
				fadeIn.RunAsync(nextIndicator, null, token),
				fadeOut.RunAsync(previousIndicator, null, token)
			);
		}

		public async void InitialFix(Rectangle initial)
		{
			if (_initialFixDone)
			{
				return;
			}

			_initialFixDone = true;

			Animation fadeIn = new()
			{
				FillMode = FillMode.Both,
				Easing = fwdEasing,
				Duration = timebase / 2,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0d),
						Setters =
						{
							new Setter(Visual.RenderTransformOriginProperty, RelativePoint.Parse("50%,100%")),
							new Setter(ScaleTransform.ScaleYProperty, 0d),
							new Setter(Visual.OpacityProperty, 0d)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(0.9999d),
						Setters =
						{
							new Setter(Visual.RenderTransformOriginProperty, RelativePoint.Parse("50%,100%"))
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(Visual.RenderTransformOriginProperty, RelativePoint.Center),
							new Setter(ScaleTransform.ScaleYProperty, 1d),
							new Setter(Visual.OpacityProperty, 1d)
						}
					}
				}
			};

			await fadeIn.RunAsync(initial, null);
		}


		public void Animate(Rectangle NextIndicator)
		{
			// For Debouncing.
			if (PreviousIndicator == NextIndicator)
			{
				return;
			}

			_currentAnimationCts?.Cancel();
			_currentAnimationCts = new CancellationTokenSource();

			AnimateIndicators(PreviousIndicator, NextIndicator,
				_currentAnimationCts.Token);

			PreviousIndicator = NextIndicator;
		}
	}
}