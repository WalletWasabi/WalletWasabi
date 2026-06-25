using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.Custom;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Behaviors;

public class BlinkOnChangeBehavior : AttachedToVisualTreeBehavior<Control>
{
	private const string BlinkClassName = "blink";

	public static readonly StyledProperty<object?> ValueProperty =
		AvaloniaProperty.Register<BlinkOnChangeBehavior, object?>(nameof(Value));

	public object? Value
	{
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		return this.GetObservable(ValueProperty)
			.Skip(1)
			.Subscribe(_ => Blink());
	}

	private async void Blink()
	{
		var control = AssociatedObject;
		if (control is null)
		{
			return;
		}

		control.Classes.Remove(BlinkClassName);
		control.Classes.Add(BlinkClassName);

		await Task.Delay(300).ConfigureAwait(false);

		Dispatcher.UIThread.Post(() => control.Classes.Remove(BlinkClassName));
	}
}
