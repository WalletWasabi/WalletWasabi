using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using AvalonStudio.MVVM;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	public partial class AddressViewModel : ViewModelBase
	{
		[AutoNotify] private string _label;
		[AutoNotify] private string _address;
		private readonly ReceiveAddressesViewModel _parent;

		public AddressViewModel(ReceiveAddressesViewModel parent, HdPubKey model, Network network,
			Func<HdPubKey, string, Task> hideCommand)
		{
			Model = model;
			_address = model.GetP2wpkhAddress(network).ToString();
			_label = model.Label;
			_parent = parent;

			CopyAddressCommand =
				ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Address));
			HideAddressCommand = ReactiveCommand.CreateFromTask(async () => await hideCommand.Invoke(model, Address));
			EditLabelCommand =
				ReactiveCommand.CreateFromTask(async () => { await _parent.NavigateToAddressEdit(this); });
		}

		public HdPubKey Model { get; }

		public ICommand CopyAddressCommand { get; }
		public ICommand HideAddressCommand { get; }
		public ICommand EditLabelCommand { get; }

		public IEnumerable<string> Labels
		{
			get => Model.Label.Labels;
			set
			{
				if (Model is null)
				{
					return;
				}

				Model.SetLabel(new SmartLabel(value), _parent.Wallet.KeyManager);
				Label = Model.Label;
			}
		}
	}
}