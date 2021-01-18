using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveAddressesViewModel : RoutableViewModel
	{
		[AutoNotify] private ObservableCollection<HdPubKey> _addresses;
		[AutoNotify] private HdPubKey? _selectedAddress;

		public ReceiveAddressesViewModel(Wallet wallet)
		{
			Wallet = wallet;
			Title = "Receive Addresses";
			_addresses = new ObservableCollection<HdPubKey>();

			InitializeAddresses();

			this.WhenAnyValue(x => x.SelectedAddress)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(selected =>
				{
					if (selected is null)
					{
						return;
					}

					Navigate().To(new ReceiveAddressViewModel(selected, wallet.Network, wallet.KeyManager.MasterFingerprint, wallet.KeyManager.IsHardwareWallet));
					SelectedAddress = null;
				});
		}

		public Wallet Wallet { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			Observable
				.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => InitializeAddresses())
				.DisposeWith(disposable);
		}

		public void InitializeAddresses()
		{
			try
			{
				Addresses.Clear();

				IEnumerable<HdPubKey> keys = Wallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Reverse();
				foreach (HdPubKey key in keys)
				{
					// addresses.Add(new AddressViewModel(key, Wallet.KeyManager, this));
					Addresses.Add(key);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}