using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<AddressViewModel> _addresses;
		private string _label;

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			_addresses = new ObservableCollection<AddressViewModel>();

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				HdPubKey newKey = Global.WalletService.GetReceiveKey(Label);

				AddressViewModel found = Addresses.FirstOrDefault(x => x.Model == newKey);
				if (found != default)
				{
					Addresses.Remove(found);
				}
				Addresses.Insert(0, new AddressViewModel(newKey));

				Label = null;
			}, this.WhenAnyValue(x => x.Label, label => !string.IsNullOrWhiteSpace(label)));
		}

		private void InitializeAddresses()
		{
			_addresses?.Clear();

			var keys = Global.WalletService.KeyManager.GetKeys(KeyState.Clean, false);

			foreach (HdPubKey key in keys.Where(x => x.HasLabel()).Reverse())
			{
				_addresses.Add(new AddressViewModel(key));
			}
		}

		public ObservableCollection<AddressViewModel> Addresses
		{
			get { return _addresses; }
			set { this.RaiseAndSetIfChanged(ref _addresses, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public ReactiveCommand GenerateCommand { get; }
	}
}
