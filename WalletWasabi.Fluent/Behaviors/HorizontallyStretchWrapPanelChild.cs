using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class HorizontallyStretchWrapPanelChild : DisposingBehavior<Control>
	{
		protected override void OnAttached(CompositeDisposable disposables)
		{
			AssociatedObject?.Parent.WhenAnyValue(x => x.Bounds)
				.DistinctUntilChanged()
				.Where(_ => AssociatedObject.Parent.VisualChildren.Count > 0)
				.Subscribe(StretchChild)
				.DisposeWith(disposables);
		}

		private void StretchChild(Rect parentBounds)
		{
			if (!(AssociatedObject?.Parent is WrapPanel parent) || AssociatedObject is null)
			{
				return;
			}

			var otherChildrenTotalWidth = parent.Children.Where(x => !Equals(x, AssociatedObject)).Sum(child => child.Margin.Left + child.Margin.Right + child.Bounds.Width);

			AssociatedObject.Width = parentBounds.Width - otherChildrenTotalWidth - AssociatedObject.Margin.Left - AssociatedObject.Margin.Right;
		}
	}
}