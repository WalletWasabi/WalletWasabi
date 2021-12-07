using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors
{
	public class ListBoxPreviewBehavior : DisposingBehavior<ListBox>
	{
		private object? _previewItem;
		private ListBoxItem? _previewControl;

		/// <summary>
		/// Defines the <see cref="SelectedItem"/> property.
		/// </summary>
		public static readonly StyledProperty<object?> PreviewItemProperty =
			AvaloniaProperty.Register<ListBoxPreviewBehavior, object?>(nameof(PreviewItem));

		public object? PreviewItem
		{
			get => GetValue(PreviewItemProperty);
			set => SetValue(PreviewItemProperty, value);
		}

		private void ClearPseudoClasses()
		{
			if (_previewControl is StyledElement se && se.Classes is IPseudoClasses pc)
			{
				pc.Remove(":previewitem");
			}
		}

		private void SetPseudoClasses()
		{
			if (_previewControl is StyledElement se && se.Classes is IPseudoClasses pc)
			{
				pc.Add(":previewitem");
			}
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerLeave))
				.Subscribe(x =>
				{
					ClearPseudoClasses();
					_previewControl = null;
					PreviewItem = null;
				}).DisposeWith(disposables);

			Observable.FromEventPattern<PointerEventArgs>(AssociatedObject, nameof(AssociatedObject.PointerMoved))
				.Subscribe(x =>
				{
					var visual = AssociatedObject.GetVisualAt(x.EventArgs.GetPosition(AssociatedObject));

					var listBoxItem = visual.FindAncestorOfType<ListBoxItem>();

					if (listBoxItem is { })
					{
						if (listBoxItem.DataContext != PreviewItem)
						{
							ClearPseudoClasses();
							_previewControl = listBoxItem;
							PreviewItem = listBoxItem.DataContext;

							SetPseudoClasses();
						}
					}
					else
					{
						ClearPseudoClasses();
						_previewControl = null;
						PreviewItem = null;
					}
				})
				.DisposeWith(disposables);
		}
	}
}