using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class HoldKeyBehavior : AttachedToVisualTreeBehavior<InputElement>
{
	public static readonly StyledProperty<Key?> KeyProperty =
		AvaloniaProperty.Register<HoldKeyBehavior, Key?>(nameof(Key));

	public static readonly StyledProperty<bool> IsKeyPressedProperty =
		AvaloniaProperty.Register<HoldKeyBehavior, bool>(nameof(IsKeyPressed), defaultBindingMode: BindingMode.TwoWay);

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
		if (AssociatedObject.GetVisualRoot() is not InputElement ie)
		{
			return;
		}

		var ups = ie.OnEvent(InputElement.KeyDownEvent);
		var downs = ie.OnEvent(InputElement.KeyUpEvent);
		var windowDeactivated = ApplicationHelper.MainWindowActivated.Where(isActivated => isActivated == false);

		var keyEvents = ups
			.Select(x => new { x.EventArgs.Key, IsPressed = true })
			.Merge(downs.Select(x => new { x.EventArgs.Key, IsPressed = false }));

		var targetKeys = this.WhenAnyValue(x => x.Key);

		var targetKeyIsPressed = keyEvents
			.WithLatestFrom(targetKeys)
			.Select(x => (PressedKey: x.First.Key, x.First.IsPressed, TargetKey: x.Second))
			.Where(x => x.PressedKey == x.TargetKey)
			.Select(x => x.IsPressed)
			.StartWith(false);

		targetKeyIsPressed
			.Merge(windowDeactivated)
			.Do(isPressed => IsKeyPressed = isPressed)
			.Subscribe()
			.DisposeWith(disposable);
	}
}
