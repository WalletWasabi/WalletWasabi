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

		protected override void OnAttached(CompositeDisposable disposables)
		{
			Observable.FromEventPattern<PointerEventArgs>(AssociatedObject, nameof(AssociatedObject.PointerMoved))
				.Subscribe(x =>
				{
					var visual = AssociatedObject.GetVisualAt(x.EventArgs.GetPosition(AssociatedObject));

					if (visual is IControl control)
					{
						if (control.DataContext != PreviewItem)
						{
							PreviewItem = control.DataContext;
						}
					}
					else
					{
						PreviewItem = null;
					}
				})
				.DisposeWith(disposables);
		}
	}
}