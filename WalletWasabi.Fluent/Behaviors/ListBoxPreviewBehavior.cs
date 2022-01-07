using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class ListBoxPreviewBehavior : DisposingBehavior<ListBox>
{
	/// <summary>
	/// Defines the <see cref="PreviewItem"/> property.
	/// </summary>
	public static readonly StyledProperty<object?> PreviewItemProperty =
		AvaloniaProperty.Register<ListBoxPreviewBehavior, object?>(nameof(PreviewItem));

	public object? PreviewItem
	{
		get => GetValue(PreviewItemProperty);
		set => SetValue(PreviewItemProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerLeave))
			.Subscribe(_ => ClearPreviewItem())
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
						PreviewItem = listBoxItem.DataContext;
					}
				}
				else
				{
					ClearPreviewItem();
				}
			})
			.DisposeWith(disposables);
	}

	private void ClearPreviewItem()
	{
		PreviewItem = null;
	}
}
