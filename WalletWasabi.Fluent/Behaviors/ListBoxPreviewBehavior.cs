using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors
{
	public class ListBoxPreviewBehavior : DisposingBehavior<ListBox>
	{
		private ListBoxItem? _previewControl;

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

		private void ClearPreviewPseudoClass(ListBoxItem? listBoxItem)
		{
			if (listBoxItem?.Classes is IPseudoClasses pc)
			{
				pc.Remove(":previewitem");
			}
		}

		private void AddPreviewPseudoClass(ListBoxItem listBoxItem)
		{
			if (listBoxItem.Classes is IPseudoClasses pc)
			{
				pc.Add(":previewitem");
			}
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
							SetPreviewItem(listBoxItem);
						}
					}
					else
					{
						ClearPreviewItem();
					}
				})
				.DisposeWith(disposables);
		}

		private void SetPreviewItem(ListBoxItem listBoxItem)
		{
			ClearPreviewPseudoClass(_previewControl);
			_previewControl = listBoxItem;
			PreviewItem = listBoxItem.DataContext;
			AddPreviewPseudoClass(_previewControl);
		}

		private void ClearPreviewItem()
		{
			ClearPreviewPseudoClass(_previewControl);
			_previewControl = null;
			PreviewItem = null;
		}
	}
}
