using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class ListBoxPreviewBehavior : DisposingBehavior<ListBox>
{
	private CancellationTokenSource _clearItemCts = new();

	/// <summary>
	/// Defines the <see cref="PreviewItem"/> property.
	/// </summary>
	public static readonly StyledProperty<object?> PreviewItemProperty =
		AvaloniaProperty.Register<ListBoxPreviewBehavior, object?>(nameof(PreviewItem));

	public static readonly StyledProperty<int> DelayProperty =
		AvaloniaProperty.Register<ListBoxPreviewBehavior, int>(nameof(Delay));

	public object? PreviewItem
	{
		get => GetValue(PreviewItemProperty);
		set => SetValue(PreviewItemProperty, value);
	}

	public int Delay
	{
		get => GetValue(DelayProperty);
		set => SetValue(DelayProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerExited))
			.Subscribe(_ => ClearPreviewItem(0))
			.DisposeWith(disposables);

		Observable.FromEventPattern<PointerEventArgs>(AssociatedObject, nameof(AssociatedObject.PointerMoved))
			.Subscribe(x =>
			{
				var visual = AssociatedObject.GetVisualAt(x.EventArgs.GetPosition(AssociatedObject));

				var listBoxItem = visual.FindAncestorOfType<ListBoxItem>();

				if (listBoxItem is { })
				{
					if (listBoxItem.DataContext != PreviewItem)
					{
						CancelClear();
						PreviewItem = listBoxItem.DataContext;
					}
				}
				else
				{
					ClearPreviewItem(Delay);
				}
			})
			.DisposeWith(disposables);
	}

	private void ClearPreviewItem(int delay)
	{
		if (delay > 0)
		{
			Observable.Timer(TimeSpan.FromMilliseconds(delay))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => PreviewItem = null, _clearItemCts.Token);
		}
		else
		{
			PreviewItem = null;
		}
	}

	protected override void OnDetachedFromVisualTree() => PreviewItem = null;

	private void CancelClear()
	{
		_clearItemCts.Cancel();
		_clearItemCts.Dispose();
		_clearItemCts = new CancellationTokenSource();
	}
}
