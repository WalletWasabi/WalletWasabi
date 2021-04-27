using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class AddressViewModel : ViewModelBase
	{
		[AutoNotify] private SmartLabel _label;
		[AutoNotify] private string _address;

		public AddressViewModel(HdPubKey model, Network network, Func<HdPubKey, string, Task> hideCommand)
		{
			Model = model;
			_address = model.GetP2wpkhAddress(network).ToString();
			_label = model.Label;

			CopyAddressCommand = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Address));
			HideAddressCommand = ReactiveCommand.CreateFromTask(async () => await hideCommand.Invoke(model, Address));
		}

		public HdPubKey Model { get; }

		public ICommand CopyAddressCommand { get; }

		public ICommand HideAddressCommand { get; }
	}
}
