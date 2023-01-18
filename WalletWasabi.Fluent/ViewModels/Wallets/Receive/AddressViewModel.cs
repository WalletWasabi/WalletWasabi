using System.Collections.Generic;
using System.Reactive;
using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class AddressViewModel : ViewModelBase
{
	[ObservableProperty] private string _address;

	public AddressViewModel(ReceiveAddressesViewModel parent, Wallet wallet, HdPubKey model, Network network)
	{
		_address = model.GetP2wpkhAddress(network).ToString();

		Label = model.Label;

		CopyAddressCommand =
			new AsyncRelayCommand(async () =>
			{
				if (Application.Current is { Clipboard: { } clipboard })
				{
					await clipboard.SetTextAsync(Address);
				}
			});

		HideAddressCommand =
			new AsyncRelayCommand(async () => await parent.HideAddressAsync(model, Address));

		EditLabelCommand =
			new RelayCommand(() => parent.NavigateToAddressEdit(model, parent.Wallet.KeyManager));

		NavigateCommand = new RelayCommand(() => parent.Navigate().To(new ReceiveAddressViewModel(wallet, model)));
	}

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ICommand NavigateCommand { get; }

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
