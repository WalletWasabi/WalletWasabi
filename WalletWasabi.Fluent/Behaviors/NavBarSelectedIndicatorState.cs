using System;
using System.Collections.Concurrent;
using System.Linq;
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
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors
{
	public class NavBarSelectedIndicatorState : IDisposable
	{
		public readonly ConcurrentDictionary<int, Control> ScopeChildren = new();
		private CancellationTokenSource _currentAnimationCts = new();
		private bool _isDispose;
		private bool _initialFixDone;
		private readonly Easing bckEasing = new SplineEasing(0.2, 1, 0.1, 0.9);
		private readonly Easing fwdEasing = new SplineEasing(0.1, 0.9, 0.2);


		private readonly TimeSpan timebase = TimeSpan.FromSeconds(0.8);
		public Rectangle? PreviousIndicator { get; set; }
		public Orientation NavItemsOrientation { get; set; } = Orientation.Vertical;

		public void Dispose()
		{
			_isDispose = true;
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

		public async void AnimateIndicators(Rectangle previousIndicator, Rectangle nextIndicator,
			CancellationToken token,
			Orientation navItemsOrientation)
		{
			if (_isDispose || previousIndicator is null || nextIndicator is null)
			{
				return;
			}

			previousIndicator.Opacity = 1;
			nextIndicator.Opacity = 0;

			var u = previousIndicator.GetVisualAncestors().OfType<StackPanel>().FirstOrDefault();

			var prevVector = previousIndicator.TranslatePoint(new Point(), u) ?? new Point();
			var nextVector = nextIndicator.TranslatePoint(new Point(), u) ?? new Point();

			var fromTopToBottom = prevVector.Y > nextVector.Y;

			var curEasing = fromTopToBottom ? fwdEasing : bckEasing;
			var direction = fromTopToBottom ? -1d : 1d;
			var newDim = Math.Abs(nextVector.Y - prevVector.Y);
			var targetY = direction * newDim;
			var maxScale = newDim / nextIndicator.Bounds.Height;

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
							new Setter(TranslateTransform.XProperty, 0d),
							new Setter(TranslateTransform.YProperty, targetY)
						}
					}
				}
			};


			Animation fadeOut = new()
			{
				Easing = new CubicEaseIn(),
				Duration = timebase / 4,
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
				Easing = new CubicEaseIn(),
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

			await Task.WhenAll(translationAnimation.RunAsync(previousIndicator, null, token),
				scalingAnimation.RunAsync(previousIndicator, null, token),
				fadeIn.RunAsync(nextIndicator, null, token),
				fadeOut.RunAsync(previousIndicator, null, token)
			);

			previousIndicator.Opacity = 0;
			nextIndicator.Opacity = 1;
		}


		public async void InitialFix(Rectangle initial)
		{
			if(_initialFixDone)
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
				_currentAnimationCts.Token, NavItemsOrientation);

			PreviousIndicator = NextIndicator;
		}
	}
}