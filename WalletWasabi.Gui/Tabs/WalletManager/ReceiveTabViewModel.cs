using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class ReceiveTabViewModel : DocumentTabViewModel
	{
		private string _walletName;
		private ObservableCollection<AddressViewModel> _addresses;
		private string _label;

		public ReceiveTabViewModel(string walletName)
		{
			_walletName = walletName;
			_addresses = new ObservableCollection<AddressViewModel>();

			InitialiseAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				var newKey = Global.WalletService.KeyManager.GenerateNewKey(Label, KeyState.Clean, false);

				Addresses.Add(new AddressViewModel(newKey));

				Label = null;
			}, this.WhenAnyValue(x => x.Label, label => !string.IsNullOrWhiteSpace(label)));
		}

		private void InitialiseAddresses()
		{
			_addresses?.Clear();

			var keys = Global.WalletService.KeyManager.GetKeys(KeyState.Clean, false);

			foreach (var key in keys)
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
