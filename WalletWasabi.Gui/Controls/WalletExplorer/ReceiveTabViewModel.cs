using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<AddressViewModel> _addresses;
		private AddressViewModel _selectedAddress;
		private string _label;
		private double _clipboardNotificationOpacity;
		private bool _clipboardNotificationVisible;

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			_addresses = new ObservableCollection<AddressViewModel>();

			Global.WalletService.Coins.CollectionChanged += Coins_CollectionChanged;

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				HdPubKey newKey = Global.WalletService.GetReceiveKey(Label);

				AddressViewModel found = Addresses.FirstOrDefault(x => x.Model == newKey);
				if (found != default)
				{
					Addresses.Remove(found);
				}

				var newAddress = new AddressViewModel(newKey);

				Addresses.Insert(0, newAddress);

				SelectedAddress = newAddress;

				Label = string.Empty;
			}, this.WhenAnyValue(x => x.Label, label => !string.IsNullOrWhiteSpace(label)));

			this.WhenAnyValue(x => x.SelectedAddress).Subscribe(async address =>
			{
				if (address != null)
				{
					await Application.Current.Clipboard.SetTextAsync(address.Address);
					ClipboardNotificationVisible = true;
					ClipboardNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						await Task.Delay(1000);
						ClipboardNotificationOpacity = 0;
					});
				}
			});
		}

		private void Coins_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			Dispatcher.UIThread.InvokeAsync(() => InitializeAddresses());
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

		public AddressViewModel SelectedAddress
		{
			get { return _selectedAddress; }
			set { this.RaiseAndSetIfChanged(ref _selectedAddress, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public double ClipboardNotificationOpacity
		{
			get { return _clipboardNotificationOpacity; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value); }
		}

		public bool ClipboardNotificationVisible
		{
			get { return _clipboardNotificationVisible; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value); }
		}

		public ReactiveCommand GenerateCommand { get; }
	}
}
