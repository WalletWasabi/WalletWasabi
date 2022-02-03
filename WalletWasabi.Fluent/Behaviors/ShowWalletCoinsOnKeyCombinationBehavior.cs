using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowWalletCoinsOnKeyCombinationBehavior : DisposingBehavior<Control>
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

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern<KeyEventArgs>(AssociatedObject, nameof(AssociatedObject.KeyDown))
			.Select(x => x.EventArgs)
			.Subscribe(e =>
			{
				EvaluateKeyPress(e.Key, isPressed: true);

				if (_key1Active && _key2Active && _key3Active && TryGetValidSelectedWallet(out var wallet))
				{
					wallet.WalletCoinsCommand.Execute(default);
				}
			})
			.DisposeWith(disposables);

		Observable
			.FromEventPattern<KeyEventArgs>(AssociatedObject, nameof(AssociatedObject.KeyUp))
			.Select(x => x.EventArgs)
			.Subscribe(e => EvaluateKeyPress(e.Key, isPressed: false))
			.DisposeWith(disposables);
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

	private bool TryGetValidSelectedWallet([NotNullWhen(true)] out WalletViewModel? wallet)
	{
		wallet = null;

		if (UiServices.WalletManager.SelectedWallet is WalletViewModel { IsLoggedIn: true, IsActive: true } walletViewModel &&
		    walletViewModel.WalletCoinsCommand.CanExecute(default))
		{
			wallet = walletViewModel;
		}

		return wallet is { };
	}
}
