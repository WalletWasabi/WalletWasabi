using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using DataBox;

namespace WalletWasabi.Fluent.Behaviors
{
	public class HistoryItemDetailsBehavior : DisposingBehavior<DataBoxRow>
	{
		private HistoryItemDetailsAdorner? _historyItemDetailsAdorner;

		protected override void OnAttached(CompositeDisposable disposables)
		{
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
