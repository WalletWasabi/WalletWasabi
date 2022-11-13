using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Views.Wallets.Receive.Columns;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Receive Addresses")]
public partial class ReceiveAddressesViewModel : RoutableViewModel
{
	private ObservableCollection<AddressViewModel> _addresses;

	public ReceiveAddressesViewModel(Wallet wallet)
	{
		Wallet = wallet;
		Network = wallet.Network;
		_addresses = new ObservableCollection<AddressViewModel>();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		// [Column]		[View]				[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Actions		ActionsColumnView	-			90			-				-			false
		// Address		AddressColumnView	Address		2*			-				-			true
		// Labels		LabelsColumnView	Labels		210			-				-			false

		Source = new FlatTreeDataGridSource<AddressViewModel>(_addresses)
		{
			Columns =
			{
				// Actions
				new TemplateColumn<AddressViewModel>(
					null,
					new FuncDataTemplate<AddressViewModel>((node, ns) => new ActionsColumnView(), true),
					options: new ColumnOptions<AddressViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false
					},
					width: new GridLength(90, GridUnitType.Pixel)),

				// Address
				new TemplateColumn<AddressViewModel>(
					"Address",
					new FuncDataTemplate<AddressViewModel>((node, ns) => new AddressColumnView(), true),
					options: new ColumnOptions<AddressViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = AddressViewModel.SortAscending(x => x.Address),
						CompareDescending = AddressViewModel.SortDescending(x => x.Address)
					},
					width: new GridLength(2, GridUnitType.Star)),

				// Labels
				new TemplateColumn<AddressViewModel>(
					"Labels",
					new FuncDataTemplate<AddressViewModel>((node, ns) => new LabelsColumnView(), true),
					options: new ColumnOptions<AddressViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = AddressViewModel.SortAscending(x => x.Label),
						CompareDescending = AddressViewModel.SortDescending(x => x.Label)
					},
					width: new GridLength(210, GridUnitType.Pixel))
			}
		};

		Source.RowSelection!.SingleSelect = true;

		InitializeAddresses();
	}

	public Wallet Wallet { get; }

	public Network Network { get; }

	public FlatTreeDataGridSource<AddressViewModel> Source { get; }

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
			_addresses.Clear();

			IEnumerable<HdPubKey> keys = Wallet.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Reverse();

			foreach (HdPubKey key in keys)
			{
				_addresses.Add(new AddressViewModel(this, Wallet, key, Network));
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
		Navigate(NavigationTarget.CompactDialogScreen).To(new AddressLabelEditViewModel(this, hdPubKey, keyManager));
	}
}
