using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowWalletCoinsOnKeyCombinationBehavior : AttachedToVisualTreeBehavior<Control>
{
	private bool _key1Active;
	private bool _key2Active;
	private bool _key3Active;

	public static readonly StyledProperty<Key?> Key1Property =
		AvaloniaProperty.Register<ShowWalletCoinsOnKeyCombinationBehavior, Key?>(nameof(Key1));

	public static readonly StyledProperty<Key?> Key2Property =
		AvaloniaProperty.Register<ShowWalletCoinsOnKeyCombinationBehavior, Key?>(nameof(Key2));

	public static readonly StyledProperty<Key?> Key3Property =
		AvaloniaProperty.Register<ShowWalletCoinsOnKeyCombinationBehavior, Key?>(nameof(Key3));

	public static readonly StyledProperty<WalletViewModel?> WalletProperty =
		AvaloniaProperty.Register<ShowWalletCoinsOnKeyCombinationBehavior, WalletViewModel?>(nameof(Wallet));

	public Key? Key1
	{
		get => GetValue(Key1Property);
		set => SetValue(Key1Property, value);
	}

	public Key? Key2
	{
		get => GetValue(Key2Property);
		set => SetValue(Key2Property, value);
	}

	public Key? Key3
	{
		get => GetValue(Key3Property);
		set => SetValue(Key3Property, value);
	}

	public WalletViewModel? Wallet
	{
		get => GetValue(WalletProperty);
		set => SetValue(WalletProperty, value);
	}

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject?.GetVisualRoot() is not InputElement inputRoot)
		{
			return Disposable.Empty;
		}

		var disposable = new CompositeDisposable();

		inputRoot.AddDisposableHandler(InputElement.KeyDownEvent, OnKeyDown).DisposeWith(disposable);
		inputRoot.AddDisposableHandler(InputElement.KeyUpEvent, OnKeyUp).DisposeWith(disposable);

		return disposable;
	}

	private void OnKeyUp(object? sender, KeyEventArgs e)
	{
		EvaluateKeyPress(e.Key, isPressed: false);
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		EvaluateKeyPress(e.Key, isPressed: true);

		if (_key1Active && _key2Active && _key3Active && Wallet is { IsActive: true })
		{
			Wallet.WalletCoinsCommand.Execute(default);
		}
	}

	private void EvaluateKeyPress(Key key, bool isPressed)
	{
		if (key == Key1)
		{
			_key1Active = isPressed;
		}

		if (key == Key2)
		{
			_key2Active = isPressed;
		}

		if (key == Key3)
		{
			_key3Active = isPressed;
		}
	}
}
