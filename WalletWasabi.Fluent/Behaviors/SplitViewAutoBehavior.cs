using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class SplitViewAutoBehavior : Behavior<SplitView>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			Disposables.Add(AssociatedObject.WhenAnyValue(x => x.Bounds)
				.DistinctUntilChanged()
				.Subscribe(SplitViewBoundsChanged));

			base.OnAttached();
		}

		private void SplitViewBoundsChanged(Rect x)
		{
			if (AssociatedObject is null)
			{
				return;
			}
			
			if (x.Width <= 650)
			{
				AssociatedObject.DisplayMode = SplitViewDisplayMode.CompactOverlay;

				if (AssociatedObject.IsPaneOpen)
				{
					AssociatedObject.IsPaneOpen = false;
				}
			}
			else
			{
				AssociatedObject.DisplayMode = SplitViewDisplayMode.CompactInline;
			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
