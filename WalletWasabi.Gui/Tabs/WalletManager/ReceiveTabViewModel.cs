using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class ReceiveTabViewModel : WasabiDocumentTabViewModel
	{
		private string _walletName;
		private ObservableCollection<AddressViewModel> _addresses;
		private string _label;

		public ReceiveTabViewModel(string walletName)
		{
			_walletName = walletName;
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

			foreach (HdPubKey key in keys.Where(x => !string.IsNullOrWhiteSpace(x.Label)).Reverse())
			{
				_addresses.Add(new AddressViewModel(key));
			}
		}

		public override string Title
		{
			get => "Receive: " + _walletName;
			set
			{
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
