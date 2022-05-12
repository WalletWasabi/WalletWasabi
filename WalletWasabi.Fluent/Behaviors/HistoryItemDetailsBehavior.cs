using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class HistoryItemDetailsBehavior : DisposingBehavior<TreeDataGridRow>
{
	private TreeDataGridItemDetailsAdorner _adorner;

	protected override void OnDetaching()
	{
		base.OnDetaching();
		RemoveAdorner();
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.Initialized))
			.Subscribe(_ => AssociatedObjectOnInitialized())
			.DisposeWith(disposables);
	}

	private static IObservable<bool> GetIsAdornerVisible(InputElement associatedObject, InputElement adorner)
	{
		var isPointerOverAdorner = adorner.GetObservable(InputElement.IsPointerOverProperty);
		var isPointerOverAssociatedObject = associatedObject.GetObservable(InputElement.IsPointerOverProperty);

		var delay = TimeSpan.FromSeconds(0.05);

		var overAssociated = isPointerOverAssociatedObject.DelayFalse(delay);
		var overAdorner = isPointerOverAdorner.DelayFalse(delay);

		return overAssociated
			.CombineLatest(overAdorner, (isOverAssociated, isOverAdorner) => isOverAssociated || isOverAdorner)
			.DistinctUntilChanged();
	}

	private void AssociatedObjectOnInitialized()
	{
		var adorner = AddAdorner(AssociatedObject);
		_adorner = adorner;

		var isAdornerVisible = GetIsAdornerVisible(AssociatedObject, adorner);

		isAdornerVisible
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(isVisible => _adorner.IsVisible = isVisible);
	}

	private AdornerCanvas? GetAdornerLayer()
	{
		if (AssociatedObject is null)
		{
			return null;
		}

		return AdornerCanvas.GetAdornerLayer(AssociatedObject);
	}

	private void RemoveAdorner()
	{
		var layer = GetAdornerLayer();

		if (layer is null)
		{
			return;
		}

		layer.Children.Remove(_adorner);
	}

	private TreeDataGridItemDetailsAdorner AddAdorner(TreeDataGridRow? to)
	{
		var layer = GetAdornerLayer();

		var adorner = new TreeDataGridItemDetailsAdorner
		{
			[AdornerCanvas.AdornedElementProperty] = to,
			[AdornerCanvas.IsClipEnabledProperty] = false,
			Row = to
		};

		var setLogicalParent = (ISetLogicalParent) adorner;
		setLogicalParent.SetParent(to);
		layer.Children.Add(adorner);
		return adorner;
	}
}