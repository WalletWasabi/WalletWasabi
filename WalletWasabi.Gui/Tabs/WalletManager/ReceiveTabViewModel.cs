using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Gui.ViewModels;

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
			_addresses = new ObservableCollection<AddressViewModel>
			{
				new AddressViewModel("Label1", "0x85858fe5d"),
				new AddressViewModel("Label2", "0x85858fe5d"),
			};

			GenerateCommand = ReactiveCommand.Create(() =>
			{
			}, this.WhenAnyValue(x => x.Label, label => !string.IsNullOrWhiteSpace(label)));
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
