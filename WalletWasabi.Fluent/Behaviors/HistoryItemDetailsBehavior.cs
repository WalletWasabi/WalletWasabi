using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using DataBox;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.Behaviors;

public class HistoryItemDetailsBehavior : DisposingBehavior<DataBoxRow>
{
	private DataBoxItemDetailsAdorner? _itemDetailsAdorner;
	private IDisposable? _currentAdornerEvents;

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is not null)
		{
			Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.DetachedFromVisualTree))
				.Subscribe(x =>
				{
					Remove();
				})
				.DisposeWith(disposables);

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
				})
				.DisposeWith(disposables);
		}
	}

	private void CheckIfShouldRemove()
	{
		Dispatcher.UIThread.Post(() =>
		{
			if (AssociatedObject is not null && _itemDetailsAdorner is not null && !AssociatedObject.IsPointerOver && !_itemDetailsAdorner.IsPointerOver)
			{
				RemoveAdorner(AssociatedObject);
			}
		});
	}

	private void Remove()
	{
		if (AssociatedObject is not null)
		{
			RemoveAdorner(AssociatedObject);
		}

		_currentAdornerEvents?.Dispose();
		_currentAdornerEvents = null;
	}

	protected override void OnDetaching()
	{
		Remove();

		base.OnDetaching();
	}

	private void AddAdorner(DataBoxRow dataBoxRow)
	{
		_currentAdornerEvents?.Dispose();
		_currentAdornerEvents = null;

		var layer = AdornerCanvas.GetAdornerLayer(dataBoxRow);
		if (layer is null || _itemDetailsAdorner is not null)
		{
			return;
		}

		_itemDetailsAdorner = new DataBoxItemDetailsAdorner
		{
			[AdornerCanvas.AdornedElementProperty] = dataBoxRow,
			[AdornerCanvas.IsClipEnabledProperty] = false,
			Row = dataBoxRow
		};

		_currentAdornerEvents = _itemDetailsAdorner.GetObservable(InputElement.IsPointerOverProperty)
			.Subscribe(_ => CheckIfShouldRemove());

		((ISetLogicalParent)_itemDetailsAdorner).SetParent(dataBoxRow);
		layer.Children.Add(_itemDetailsAdorner);
	}

	private void RemoveAdorner(DataBoxRow dataBoxRow)
	{
		_currentAdornerEvents?.Dispose();
		_currentAdornerEvents = null;

		var layer = AdornerCanvas.GetAdornerLayer(dataBoxRow);
		if (layer is null || _itemDetailsAdorner is null)
		{
			return;
		}

		layer.Children.Remove(_itemDetailsAdorner);
		((ISetLogicalParent)_itemDetailsAdorner).SetParent(null);
		_itemDetailsAdorner = null;
	}
}
