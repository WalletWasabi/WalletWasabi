using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(Title = "Receive Addresses")]
	public partial class ReceiveAddressesViewModel : RoutableViewModel
	{
		[AutoNotify] private ObservableCollection<AddressViewModel> _addresses;
		[AutoNotify] private HdPubKey? _selectedAddress;

		public ReceiveAddressesViewModel(Wallet wallet)
		{
			Wallet = wallet;
			Network = wallet.Network;
			_addresses = new ObservableCollection<AddressViewModel>();

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

		public Network Network { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(inStack, disposables);

			Observable
				.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => InitializeAddresses())
				.DisposeWith(disposables);
		}

		public void InitializeAddresses()
		{
			try
			{
				Addresses.Clear();

				IEnumerable<HdPubKey> keys = Wallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Reverse();

				foreach (HdPubKey key in keys)
				{
					Addresses.Add(new AddressViewModel(key, Network));
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
