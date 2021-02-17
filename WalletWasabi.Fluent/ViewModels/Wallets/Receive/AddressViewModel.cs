using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class AddressViewModel : ViewModelBase
	{
		[AutoNotify] private string _label;
		[AutoNotify] private string _address;

		public AddressViewModel(HdPubKey model, Network network)
		{
			_address = model.GetP2wpkhAddress(network).ToString();
			_label = model.Label;

			CopyAddressCommand = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Address));
		}

		public ICommand CopyAddressCommand { get; }
	}
}
