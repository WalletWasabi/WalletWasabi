using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

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
			.Subscribe(
				_ =>
				{
					var parentControl = AssociatedObject.FindLogicalAncestorOfType<ItemsControl>();

					if (parentControl is { })
					{
						foreach (var item in parentControl.GetLogicalChildren())
						{
							var nextToFocus = item.FindLogicalDescendantOfType<TextBox>();

							if (nextToFocus.IsEnabled)
							{
								nextToFocus.Focus();
								return;
							}
						}

						parentControl.Focus();
					}
				})
			.DisposeWith(disposables);
	}
}
