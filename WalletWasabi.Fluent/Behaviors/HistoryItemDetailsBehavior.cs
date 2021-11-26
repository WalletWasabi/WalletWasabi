using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using DataBox;

namespace WalletWasabi.Fluent.Behaviors
{
	public class HistoryItemDetailsBehavior : DisposingBehavior<DataBoxRow>
	{
		private HistoryItemDetailsAdorner? _historyItemDetailsAdorner;
		private IDisposable? _currentAdornerEvents;

		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is not null)
			{
				disposables.Add(
					AssociatedObject
						.GetObservable(InputElement.IsPointerOverProperty)
						.Subscribe(x =>
						{
							if (AssociatedObject.IsPointerOver)
							{
								AddAdorner(AssociatedObject);
							}
							else
							{
								CheckIfShouldRemove();
							}
						}));

				disposables.Add(
					AssociatedObject
						.GetObservable(ListBoxItem.IsSelectedProperty)
						.Subscribe(x =>
						{
							if (AssociatedObject.IsSelected)
							{
								AddAdorner(AssociatedObject);
							}
							else
							{
								CheckIfShouldRemove();
							}
						}));
			}
		}

		private void CheckIfShouldRemove()
		{
			Dispatcher.UIThread.Post(() =>
			{
				if (_historyItemDetailsAdorner != null && !AssociatedObject.IsPointerOver &&
				    !_historyItemDetailsAdorner.IsPointerOver)
				{
					RemoveAdorner(AssociatedObject);
				}
			});
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			if (AssociatedObject is not null)
			{
				RemoveAdorner(AssociatedObject);
			}

			_currentAdornerEvents?.Dispose();
			_currentAdornerEvents = null;
		}

		private void AddAdorner(DataBoxRow dataBoxRow)
		{
			_currentAdornerEvents?.Dispose();
			_currentAdornerEvents = null;

			var layer = AdornerLayer.GetAdornerLayer(dataBoxRow);
			if (layer is null || _historyItemDetailsAdorner is not null)
			{
				return;
			}

			_historyItemDetailsAdorner = new HistoryItemDetailsAdorner
			{
				[AdornerLayer.AdornedElementProperty] = dataBoxRow,
				[AdornerLayer.IsClipEnabledProperty] = false,
				Row = dataBoxRow
			};

			_currentAdornerEvents = _historyItemDetailsAdorner.GetObservable(InputElement.IsPointerOverProperty)
				.Subscribe(_ => CheckIfShouldRemove());

			((ISetLogicalParent)_historyItemDetailsAdorner).SetParent(dataBoxRow);
			layer.Children.Add(_historyItemDetailsAdorner);
		}

		private void RemoveAdorner(DataBoxRow dataBoxRow)
		{
			_currentAdornerEvents?.Dispose();
			_currentAdornerEvents = null;

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
