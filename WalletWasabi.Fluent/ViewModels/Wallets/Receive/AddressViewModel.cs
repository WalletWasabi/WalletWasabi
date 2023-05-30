using System.Reactive;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class AddressViewModel : ViewModelBase
{
	[AutoNotify] private string _address;

	private AddressViewModel(ReceiveAddressesViewModel parent, Wallet wallet, HdPubKey model, Network network)
	{
		_address = model.GetP2wpkhAddress(network).ToString();

		Labels = model.Labels;

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

		NavigateCommand = ReactiveCommand.Create(() => parent.Navigate().To(new ReceiveAddressViewModel(UiContext, new WalletModel(wallet), new Address(wallet.KeyManager, model), Services.UiConfig.Autocopy)));
	}

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

	public LabelsArray Labels { get; }
}
