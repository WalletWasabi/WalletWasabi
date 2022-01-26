using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Receive Addresses")]
public partial class ReceiveAddressesViewModel : RoutableViewModel
{
	private readonly HashSet<string> _suggestions;

	[AutoNotify] private ObservableCollection<AddressViewModel> _addresses;
	[AutoNotify] private AddressViewModel? _selectedAddress;

	public ReceiveAddressesViewModel(Wallet wallet, HashSet<string> suggestions)
	{
		_suggestions = suggestions;
		Wallet = wallet;
		Network = wallet.Network;
		_addresses = new ObservableCollection<AddressViewModel>();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		InitializeAddresses();
	}

	public Wallet Wallet { get; }

	public Network Network { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

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
				Addresses.Add(new AddressViewModel(this, Wallet, key, Network));
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public async Task HideAddressAsync(HdPubKey model, string address)
	{
		var result = await NavigateDialogAsync(new ConfirmHideAddressViewModel(model.Label));

		if (result.Result == false)
		{
			return;
		}

		model.SetKeyState(KeyState.Locked, Wallet.KeyManager);
		InitializeAddresses();

		if (Application.Current is { Clipboard: { } clipboard })
		{
			var isAddressCopied = await clipboard.GetTextAsync() == address;

			if (isAddressCopied)
			{
				await clipboard.ClearAsync();
			}
		}
	}

	public void NavigateToAddressEdit(HdPubKey hdPubKey, KeyManager keyManager)
	{
		Navigate(NavigationTarget.CompactDialogScreen).To(new AddressLabelEditViewModel(this, hdPubKey, keyManager, _suggestions));
	}
}
