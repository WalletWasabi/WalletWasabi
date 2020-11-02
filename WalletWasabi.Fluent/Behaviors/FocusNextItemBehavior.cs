using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors
{
	internal class FocusNextItemBehavior : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<bool> IsFocusedProperty =
			AvaloniaProperty.Register<FocusNextItemBehavior, bool>(nameof(IsFocused), true);

		public bool IsFocused
		{
			get => GetValue(IsFocusedProperty);
			set => SetValue(IsFocusedProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.IsFocused)
				.Where(x => x == false)
				.Subscribe(_ =>
				{
					var parentControl = AssociatedObject.FindAncestorOfType<ItemsControl>();

					foreach (var item in parentControl.GetLogicalChildren())
					{
						var nextToFocus = item.FindLogicalDescendantOfType<TextBox>();

						if (nextToFocus.IsEnabled)
						{
							nextToFocus.Focus();
							return;
						}
					}
				})
				.DisposeWith(disposables);
		}
	}
}