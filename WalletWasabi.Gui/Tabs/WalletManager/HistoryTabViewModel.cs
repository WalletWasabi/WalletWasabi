using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class HistoryTabViewModel : DocumentTabViewModel
	{
		private string _walletName;
		private ObservableCollection<AddressViewModel> _addresses;
		private string _label;

		public HistoryTabViewModel(string walletName)
		{
			_walletName = walletName;
			_addresses = new ObservableCollection<AddressViewModel>();

			InitialiseAddresses();
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
			get => "History: " + _walletName;
			set
			{
			}
		}

		public ObservableCollection<AddressViewModel> Addresses
		{
			get { return _addresses; }
			set { this.RaiseAndSetIfChanged(ref _addresses, value); }
		}
	}
}
