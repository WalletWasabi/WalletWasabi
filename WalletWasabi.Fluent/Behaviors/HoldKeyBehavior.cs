using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class HoldKeyBehavior : AttachedToVisualTreeBehavior<InputElement>
{
	public static readonly StyledProperty<Key?> KeyProperty =
		AvaloniaProperty.Register<HoldKeyBehavior, Key?>(nameof(Key));

	public static readonly StyledProperty<bool> IsKeyPressedProperty = AvaloniaProperty.Register<HoldKeyBehavior, bool>(nameof(IsKeyPressed), defaultBindingMode: BindingMode.TwoWay);

	public Key? Key
	{
		get => GetValue(KeyProperty);
		set => SetValue(KeyProperty, value);
	}

	public bool IsKeyPressed
	{
		get => GetValue(IsKeyPressedProperty);
		set => SetValue(IsKeyPressedProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject.GetVisualRoot() is not IInputElement ie)
		{
			return;
		}

		var ups = ie.OnEvent(InputElement.KeyDownEvent);
		var downs = ie.OnEvent(InputElement.KeyUpEvent);

		var pressedKeys = ups
			.Select(x => new { x.EventArgs.Key, IsPressed = true })
			.Merge(downs.Select(x => new { x.EventArgs.Key, IsPressed = false }));

		var targetKeys = this.WhenAnyValue(x => x.Key);

		pressedKeys
			.WithLatestFrom(targetKeys, (pressedKey, targetKey) => new { pressedKey.IsPressed, TargetKey = pressedKey.Key, PressedKey = targetKey })
			.Where(x => x.PressedKey == x.TargetKey)
			.Select(x => x.IsPressed)
			.StartWith(false)
			.Do(b => IsKeyPressed = b)
			.Subscribe()
			.DisposeWith(disposable);
	}
}
