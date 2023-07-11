using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using AddressFunc = System.Func<WalletWasabi.Fluent.Models.Wallets.IAddress, System.Threading.Tasks.Task>;
using AddressAction = System.Action<WalletWasabi.Fluent.Models.Wallets.IAddress>;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class AddressViewModel : ViewModelBase
{
	[AutoNotify] private string _addressText;
	[AutoNotify] private LabelsArray _labels;

	public AddressViewModel(UiContext context, AddressFunc onEdit, AddressAction onShow, IAddress address)
	{
		UiContext = context;
		Address = address;
		_addressText = address.Text;

		this.WhenAnyValue(x => x.Address.Labels).BindTo(this, viewModel => viewModel.Labels);

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() => UiContext.Clipboard.SetTextAsync(AddressText));
		HideAddressCommand = ReactiveCommand.CreateFromTask(PromptHideAddressAsync);
		EditLabelCommand = ReactiveCommand.CreateFromTask(() => onEdit(address));
		NavigateCommand = ReactiveCommand.Create(() => onShow(address));
	}

	private IAddress Address { get; }

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

	private async Task PromptHideAddressAsync()
	{
		var result = await UiContext.Navigate().NavigateDialogAsync(new ConfirmHideAddressViewModel(Address.Labels));

		if (result.Result == false)
		{
			return;
		}

		Address.Hide();

		var isAddressCopied = await UiContext.Clipboard.GetTextAsync() == Address.Text;

		if (isAddressCopied)
		{
			await UiContext.Clipboard.ClearAsync();
		}
	}
}
