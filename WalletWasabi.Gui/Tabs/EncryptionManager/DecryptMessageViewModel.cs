using Avalonia.Diagnostics.ViewModels;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Tabs.EncryptionManager;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class DecryptMessageViewModel : CategoryViewModel, IDisposable
	{
		private string _encryptedMessage;
		private string _password;
		private string _decryptedMessage;
		private string _myPublicKey;
		private string _warningMessage;
		private bool _isPublicKeyPresent;
		private bool _isSearchResultExpanded;
		private ObservableCollection<AddressPubKeyViewModel> _addresses;
		private AddressPubKeyViewModel _selectedItem;
		private IEnumerable<AddressPubKeyViewModel> _allKeysViewModels;
		private string _addressSearch;
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		public string EncryptedMessage
		{
			get => _encryptedMessage;
			set => this.RaiseAndSetIfChanged(ref _encryptedMessage, value);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string MyPublicKey
		{
			get => _myPublicKey;
			set => this.RaiseAndSetIfChanged(ref _myPublicKey, value);
		}

		public string DecryptedMessage
		{
			get => _decryptedMessage;
			set => this.RaiseAndSetIfChanged(ref _decryptedMessage, value);
		}

		public string AddressSearch
		{
			get => _addressSearch;
			set => this.RaiseAndSetIfChanged(ref _addressSearch, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public bool IsPublicKeyPresent
		{
			get => _isPublicKeyPresent;
			set => this.RaiseAndSetIfChanged(ref _isPublicKeyPresent, value);
		}

		public bool IsSearchResultExpanded
		{
			get => _isSearchResultExpanded;
			set => this.RaiseAndSetIfChanged(ref _isSearchResultExpanded, value);
		}

		public AddressPubKeyViewModel SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		public ObservableCollection<AddressPubKeyViewModel> Addresses
		{
			get => _addresses;
			set => this.RaiseAndSetIfChanged(ref _addresses, value);
		}

		public ReactiveCommand DecryptCommand { get; }
		public ReactiveCommand MyPublicKeyCommand { get; }

		public EncryptionManagerViewModel Owner { get; }

		public DecryptMessageViewModel(EncryptionManagerViewModel owner) : base("Decrypt Message")
		{
			Owner = owner;

			var canDecrypt = this.WhenAnyValue(x => x.EncryptedMessage, x => x.MyPublicKey,
				(message, pubkey) =>
					!string.IsNullOrEmpty(message) &&
					!string.IsNullOrEmpty(pubkey));

			DecryptCommand = ReactiveCommand.Create(
				() =>
				{
					WarningMessage = "";
					DecryptedMessage = DecryptMessage(EncryptedMessage, MyPublicKey, Password);
				},
				canDecrypt
			);

			DecryptCommand.ThrownExceptions.Subscribe(ex =>
			{
				ShowWarnMessage($"Decryption failed. Please check the correctness of Encrypted message, Public Key and Password! Details: {ex.Message}");
			});

			MyPublicKeyCommand = ReactiveCommand.Create(
				() =>
				{
					WarningMessage = "";
					DecryptedMessage = DecryptMessage(EncryptedMessage, MyPublicKey, Password);
				},
				canDecrypt
			);

			MyPublicKeyCommand.ThrownExceptions.Subscribe(ex =>
			{
				ShowWarnMessage(ex.Message);
			});

			this.WhenAnyValue(x => x.SelectedItem)
				.Subscribe((x) => MyPublicKey = x is null ? "" : x.PubKey);

			this.WhenAnyValue(x => x.AddressSearch)
				.Throttle(TimeSpan.FromMilliseconds(500)).Subscribe((searchText) =>
			   {
				   if (string.IsNullOrEmpty(searchText))
					   Addresses = new ObservableCollection<AddressPubKeyViewModel>(_allKeysViewModels);
				   else
				   {
					   Addresses = new ObservableCollection<AddressPubKeyViewModel>(_allKeysViewModels.Where(vm => vm.Address.Contains(searchText)));
					   if (Addresses.Count == 1)
					   {
						   SelectedItem = Addresses.First();
					   }

					   IsSearchResultExpanded = Addresses.Any();
				   }
			   });

			IEnumerable<HdPubKey> keys = Global.WalletService.KeyManager.GetKeys();
			_allKeysViewModels = keys.Select(a => new AddressPubKeyViewModel(a));
		}

		private void ShowWarnMessage(string message)
		{
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = new CancellationTokenSource();
			WarningMessage = message;
			Dispatcher.UIThread.Post(async () =>
			{
				try
				{
					await Task.Delay(7000, _cancellationTokenSource.Token);
					WarningMessage = "";
				}
				catch (Exception) { };
			});
		}

		private string DecryptMessage(string message, string pubKeyHex, string password)
		{
			password = Guard.Correct(password);
			PubKey pk = new PubKey(pubKeyHex);
			HdPubKey hdPubKey = Global.WalletService.KeyManager.GetKeys().FirstOrDefault(k => k.PubKey == pk);
			if (hdPubKey == null)
			{
				throw new InvalidOperationException("Public key does not belong to the wallet.");
			}

			(ExtKey secret, HdPubKey pubKey) extKey = Global.WalletService.KeyManager.GetSecretsAndPubKeyPairs(password, hdPubKey.PubKey.ScriptPubKey).FirstOrDefault();
			if (extKey.Equals(default))
			{
				throw new InvalidOperationException("Could not find the corresponding extKey in your wallet for that ScriptPubKey");
			}
			return extKey.secret.PrivateKey.Decrypt(message);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false;

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_cancellationTokenSource.Cancel();
					_cancellationTokenSource.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
