using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class AddressViewModel : ViewModelBase
{
	[AutoNotify] private string _address;

	public AddressViewModel(ReceiveAddressesViewModel parent, Wallet wallet, HdPubKey model, Network network)
	{
		_address = model.GetP2wpkhAddress(network).ToString();

		Label = model.Label;

		CopyAddressCommand =
			ReactiveCommand.CreateFromTask(async () =>
			{
				if (Application.Current is { Clipboard: { } clipboard })
				{
					await clipboard.SetTextAsync(Address);
				}
			});

		HideAddressCommand =
			ReactiveCommand.CreateFromTask(async () => await parent.HideAddressAsync(model, Address));

		EditLabelCommand =
			ReactiveCommand.Create(() => parent.NavigateToAddressEdit(model, parent.Wallet.KeyManager));

		NavigateCommand = ReactiveCommand.Create(() => parent.Navigate().To(new ReceiveAddressViewModel(wallet, model)));
	}

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

	public SmartLabel Label { get; }

	public static Comparison<AddressViewModel?> SortAscending<T>(Func<AddressViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return -1;
			}
			else if (y is null)
			{
				return 1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(x), selector(y));
			}
		};
	}

	public static Comparison<AddressViewModel?> SortDescending<T>(Func<AddressViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return 1;
			}
			else if (y is null)
			{
				return -1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(y), selector(x));
			}
		};
	}
}
