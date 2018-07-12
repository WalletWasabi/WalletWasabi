﻿using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
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
		private string _warningMessage;

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			_addresses = new ObservableCollection<AddressViewModel>();

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
			{
				InitializeAddresses();
			});

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				if (string.IsNullOrWhiteSpace(Label))
				{
					WarningMessage = "Label cannot be empty";
				}
				else
				{
					WarningMessage = string.Empty;

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
				}
			});

			this.WhenAnyValue(x => x.Label).Subscribe(label => { WarningMessage = string.Empty; });

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

		public string WarningMessage
		{
			get { return _warningMessage; }
			set { this.RaiseAndSetIfChanged(ref _warningMessage, value); }
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
