using System.Reactive;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class AddressViewModel : ViewModelBase
	{
		[AutoNotify] private SmartLabel _label;
		[AutoNotify] private string _address;

		public AddressViewModel(ReceiveAddressesViewModel parent, HdPubKey model, Network network, UiConfig uiConfig)
		{
			_address = model.GetP2wpkhAddress(network).ToString();
			_label = model.Label;

			CopyAddressCommand =
				ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Address));
			HideAddressCommand =
				ReactiveCommand.CreateFromTask(async () => await parent.HideAddressAsync(model, Address));
			EditLabelCommand =
				ReactiveCommand.Create(() => parent.NavigateToAddressEdit(model, parent.Wallet.KeyManager));

			NavigateCommand = ReactiveCommand.Create(() =>
			{
				parent.Navigate().To(
					new ReceiveAddressViewModel(
						model,
						network,
						parent.Wallet.KeyManager.MasterFingerprint,
						parent.Wallet.KeyManager.IsHardwareWallet,
						uiConfig));
			});
		}

		public ICommand CopyAddressCommand { get; }

		public ICommand HideAddressCommand { get; }

		public ICommand EditLabelCommand { get; }

		public ReactiveCommand<Unit, Unit> NavigateCommand { get; }
	}
}
