using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using DataBox;

namespace WalletWasabi.Fluent.Behaviors
{
	public class HistoryItemDetailsAdorner : TemplatedControl
	{
		public static readonly StyledProperty<DataBoxRow?> RowProperty =
			AvaloniaProperty.Register<HistoryItemDetailsAdorner, DataBoxRow?>(nameof(Row));

		public DataBoxRow? Row
		{
			get => GetValue(RowProperty);
			set => SetValue(RowProperty, value);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (Row is null)
			{
				return availableSize;
			}

			var bounds = Row.Bounds;

			availableSize.WithHeight(bounds.Height);

			var result = availableSize;

			foreach (var visualChild in VisualChildren)
			{
				if (visualChild is Control control)
				{
					control.Measure(availableSize);
					result = control.DesiredSize;
				}
			}

			return result;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (Row is null)
			{
				return finalSize;
			}

			var bounds = Row.Bounds;

			finalSize.WithHeight(bounds.Height);

			var rect = new Rect(-finalSize.Width, 0, finalSize.Width, bounds.Height);

			var result = finalSize;

			foreach (var visualChild in VisualChildren)
			{
				if (visualChild is Control control)
				{
					control.Arrange(rect);
					result = control.DesiredSize;
				}
			}

			return result;
		}
	}

	public class HistoryItemDetailsBehavior : DisposingBehavior<DataBoxRow>
	{
		private HistoryItemDetailsAdorner? _historyItemDetailsAdorner;

		protected override void OnAttached(CompositeDisposable disposables)
		{
			base.OnAttached();

			if (AssociatedObject is not null)
			{
				disposables.Add(
					AssociatedObject
						.GetObservable(InputElement.IsPointerOverProperty)
						.Subscribe(x =>
						{
							if (x)
							{
								AddAdorner(AssociatedObject);
							}
							else
							{
								RemoveAdorner(AssociatedObject);
							}
						}));
			}
		}
		private void AddAdorner(DataBoxRow dataBoxRow)
		{
			var layer = AdornerLayer.GetAdornerLayer(dataBoxRow);
			if (layer is null)
			{
				return;
			}

			_historyItemDetailsAdorner = new HistoryItemDetailsAdorner
			{
				[AdornerLayer.AdornedElementProperty] = dataBoxRow,
				IsHitTestVisible = true,
				ClipToBounds = false,
				Row = dataBoxRow
			};

			((ISetLogicalParent)_historyItemDetailsAdorner).SetParent(dataBoxRow);
			layer.Children.Add(_historyItemDetailsAdorner);
		}

		private void RemoveAdorner(DataBoxRow dataBoxRow)
		{
			var layer = AdornerLayer.GetAdornerLayer(dataBoxRow);
			if (layer is null || _historyItemDetailsAdorner is null)
			{
				return;
			}

			layer.Children.Remove(_historyItemDetailsAdorner);
			((ISetLogicalParent)_historyItemDetailsAdorner).SetParent(null);
			_historyItemDetailsAdorner = null;
		}
	}
}
