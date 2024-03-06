using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class BindPointerOverBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<bool> IsPointerOverProperty =
		AvaloniaProperty.Register<BindPointerOverBehavior, bool>(nameof(IsPointerOver), defaultBindingMode: BindingMode.TwoWay);

	public bool IsPointerOver
	{
		get => GetValue(IsPointerOverProperty);
		set => SetValue(IsPointerOverProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern<AvaloniaPropertyChangedEventArgs>(AssociatedObject, nameof(PropertyChanged))
			.Select(x => x.EventArgs)
			.Subscribe(e =>
			{
				if (e.Property == InputElement.IsPointerOverProperty)
				{
					IsPointerOver = e.NewValue is true;
				}
			})
			.DisposeWith(disposables);

		disposables.Add(Disposable.Create(() => IsPointerOver = false));
	}
}
